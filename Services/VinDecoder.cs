using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CarReliability.Data;
using CarReliability.Models;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Services;

public class VinResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }

    // Какво върна публичната база NHTSA vPIC
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public double? DisplacementL { get; set; }
    public string? Fuel { get; set; }
    public string? Drive { get; set; }
    public string? Transmission { get; set; }
    public string? Body { get; set; }
    public int? PowerHp { get; set; }

    // Съответствие в нашия каталог
    public int? BrandId { get; set; }
    public int? ModelId { get; set; }
    public int? GenerationId { get; set; }
    public string? EngineName { get; set; }
    public int? CarEngineId { get; set; }
}

/// <summary>Разчита VIN номер чрез публичната база NHTSA vPIC (Министерство на транспорта на САЩ)
/// и намира съответната марка/модел/поколение/двигател в каталога.</summary>
public class VinDecoder
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _db;

    public VinDecoder(IHttpClientFactory httpFactory, IConfiguration config, ApplicationDbContext db)
    {
        _httpFactory = httpFactory;
        _config = config;
        _db = db;
    }

    public async Task<VinResult> DecodeAsync(string vin, CancellationToken token = default)
    {
        vin = Regex.Replace(vin ?? string.Empty, @"[^A-Za-z0-9*]", "").ToUpperInvariant();
        if (vin.Length < 11 || vin.Length > 17)
            return new VinResult { Error = "VIN номерът трябва да е 17 символа (латински букви и цифри)." };

        var template = _config["PublicData:VinDecoderUrl"] ?? "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{0}?format=json";
        var result = new VinResult();
        try
        {
            var http = _httpFactory.CreateClient("public-data");
            http.Timeout = TimeSpan.FromSeconds(20);
            using var doc = JsonDocument.Parse(await http.GetStringAsync(string.Format(template, Uri.EscapeDataString(vin)), token));
            if (!doc.RootElement.TryGetProperty("Results", out var results) || results.GetArrayLength() == 0)
                return new VinResult { Error = "Публичната база не върна резултат." };
            var r = results[0];

            string? Get(string name)
            {
                foreach (var p in r.EnumerateObject())
                    if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        var v = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                    }
                return null;
            }

            result.Make = Get("Make");
            result.Model = Get("Model");
            result.Year = int.TryParse(Get("ModelYear"), out var y) ? y : null;
            result.DisplacementL = double.TryParse(Get("DisplacementL"), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 1) : null;
            result.Fuel = Get("FuelTypePrimary");
            result.Drive = Get("DriveType");
            result.Transmission = string.Join(" ", new[] { Get("TransmissionSpeeds"), Get("TransmissionStyle") }.Where(s => s != null));
            result.Body = Get("BodyClass");
            result.PowerHp = double.TryParse(Get("EngineHP"), NumberStyles.Float, CultureInfo.InvariantCulture, out var hp) ? (int)Math.Round(hp) : null;

            if (string.IsNullOrEmpty(result.Make))
            {
                result.Error = "VIN номерът не беше разпознат. Проверете го или изберете колата ръчно.";
                return result;
            }
        }
        catch (Exception)
        {
            return new VinResult { Error = "Няма връзка с публичната база NHTSA. Проверете интернет връзката или изберете колата ръчно." };
        }

        await MatchCatalogAsync(result, token);
        result.Ok = true;
        return result;
    }

    private async Task MatchCatalogAsync(VinResult r, CancellationToken token)
    {
        var make = Norm(r.Make!);
        var brands = await _db.CarBrands.ToListAsync(token);
        var brand = brands.FirstOrDefault(b => Norm(b.Name) == make) ?? brands.FirstOrDefault(b => make.StartsWith(Norm(b.Name)));
        if (brand == null) return;
        r.BrandId = brand.Id;
        if (string.IsNullOrEmpty(r.Model)) return;

        var modelName = Norm(r.Model);
        var models = await _db.CarModels.Where(m => m.CarBrandId == brand.Id).ToListAsync(token);
        var model = models.FirstOrDefault(m => Norm(m.Name) == modelName)
                    ?? models.Where(m => modelName.StartsWith(Norm(m.Name)) || Norm(m.Name).StartsWith(modelName))
                             .OrderByDescending(m => m.Name.Length).FirstOrDefault();
        if (model == null) return;
        r.ModelId = model.Id;
        if (!r.Year.HasValue) return;

        var year = r.Year.Value;
        var gens = await _db.CarGenerations.Include(g => g.Engines)
            .Where(g => g.CarModelId == model.Id && g.YearFrom <= year && g.YearTo >= year)
            .ToListAsync(token);
        if (gens.Count == 0) return;

        // предпочитаме поколение, в което има двигател със същия обем
        CarEngine? best = null;
        CarGeneration? bestGen = null;
        double bestScore = double.MaxValue;
        foreach (var g in gens)
        {
            foreach (var e in g.Engines.Where(e => (e.YearFrom ?? year) <= year && (e.YearTo ?? year) >= year))
            {
                var score = 0.0;
                if (r.DisplacementL.HasValue)
                    score += e.DisplacementCc.HasValue ? Math.Abs(e.DisplacementCc.Value / 1000.0 - r.DisplacementL.Value) * 10 : 5;
                if (r.Fuel != null) score += FuelMatches(e.FuelType, r.Fuel) ? 0 : 3;
                if (r.PowerHp.HasValue && e.PowerHp.HasValue) score += Math.Abs(e.PowerHp.Value - r.PowerHp.Value) / 20.0;
                if (r.Drive != null) score += DriveMatches(e.DriveType, r.Drive) ? 0 : 1;
                if (r.Transmission != null) score += e.Transmission.Contains(r.Transmission.Contains("Manual", StringComparison.OrdinalIgnoreCase) ? "ръчна" : "автомат", StringComparison.OrdinalIgnoreCase) ? 0 : 0.5;
                if (g.Source == CatalogSources.AutoEvolution) score -= 0.1;
                if (score < bestScore) { bestScore = score; best = e; bestGen = g; }
            }
        }

        bestGen ??= gens.OrderBy(g => g.Source == CatalogSources.AutoEvolution ? 0 : 1).First();
        r.GenerationId = bestGen.Id;
        if (best != null && (r.DisplacementL.HasValue || r.Fuel != null))
        {
            r.EngineName = best.Name;
            r.CarEngineId = best.Id;
        }
    }

    private static bool FuelMatches(FuelType fuel, string vpicFuel)
    {
        var f = vpicFuel.ToLowerInvariant();
        return fuel switch
        {
            FuelType.Diesel => f.Contains("diesel"),
            FuelType.Electric => f.Contains("electric"),
            FuelType.CNG => f.Contains("natural gas"),
            FuelType.LPG => f.Contains("propane") || f.Contains("lpg"),
            _ => f.Contains("gasoline") || f.Contains("petrol")
        };
    }

    private static bool DriveMatches(string drive, string vpicDrive)
    {
        var d = vpicDrive.ToLowerInvariant();
        return drive switch
        {
            "Предно" => d.Contains("fwd") || d.Contains("front"),
            "Задно" => d.Contains("rwd") || d.Contains("rear"),
            "4x4" => d.Contains("awd") || d.Contains("4wd") || d.Contains("4x4") || d.Contains("all"),
            _ => false
        };
    }

    private static string Norm(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9а-я]", "");
}
