using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CarReliability.Data;
using CarReliability.Models;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Services;

/// <summary>
/// Внася автомобили от публичната база данни на FuelEconomy.gov (Агенция за опазване на околната среда на САЩ, EPA):
/// всички модели, продавани в САЩ от 1995 г. насам — марка, модел, година, двигател, скоростна кутия, задвижване, гориво.
/// Данните се добавят към същите марки/модели от основния каталог (отбелязани с „САЩ“).
/// Изтегля се един архив (vehicles.csv.zip, ~2 MB) директно от сайта на EPA.
/// </summary>
public class PublicCatalogImporter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PublicCatalogImporter> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PublicCatalogImporter(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        IConfiguration config, ILogger<PublicCatalogImporter> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public bool IsRunning => _lock.CurrentCount == 0;

    /// <summary>Стартира импорта във фонов режим (ако вече не работи).</summary>
    public bool StartInBackground()
    {
        if (IsRunning) return false;
        _ = Task.Run(() => RunAsync(CancellationToken.None));
        return true;
    }

    public async Task RunAsync(CancellationToken token)
    {
        if (!await _lock.WaitAsync(0, token)) return;
        CatalogImport? log = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            log = new CatalogImport { Source = "FuelEconomy.gov (EPA, САЩ)", StartedAt = DateTime.UtcNow };
            db.CatalogImports.Add(log);
            await db.SaveChangesAsync(token);

            var url = _config["PublicData:EpaVehiclesUrl"] ?? "https://www.fueleconomy.gov/feg/epadata/vehicles.csv.zip";
            var minYear = int.TryParse(_config["PublicData:EpaMinYear"], out var y) ? y : 1995;

            _logger.LogInformation("Изтегляне на публичната база с автомобили от {Url} ...", url);
            var rows = await DownloadRowsAsync(url, minYear, token);
            _logger.LogInformation("Прочетени {Count} записа от EPA. Добавяне в каталога...", rows.Count);

            await MergeAsync(db, rows, log, token);

            log.Status = "Успешен";
            log.FinishedAt = DateTime.UtcNow;
            log.Message = $"Прочетени {rows.Count} записа (модели от {minYear} г. насам).";
            await db.SaveChangesAsync(token);
            _logger.LogInformation("Импортът от EPA приключи: +{B} марки, +{M} модела, +{G} версии, +{E} двигателя.",
                log.BrandsAdded, log.ModelsAdded, log.GenerationsAdded, log.EnginesAdded);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Импортът от публичната база не успя (няма интернет или сайтът не отговаря).");
            if (log != null)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var entry = await db.CatalogImports.FindAsync(log.Id);
                    if (entry != null)
                    {
                        entry.Status = "Грешка";
                        entry.FinishedAt = DateTime.UtcNow;
                        entry.Message = Cut(ex.Message, 1000);
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                }
                catch { /* базата не е достъпна */ }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // ---------- 1. Изтегляне и четене на CSV ----------

    private record EpaRow(string Make, string Family, string Variant, int Year, string Engine, FuelType Fuel,
        int? Cc, string Transmission, string Drive, BodyType Body);

    private async Task<List<EpaRow>> DownloadRowsAsync(string url, int minYear, CancellationToken token)
    {
        var http = _httpFactory.CreateClient("public-data");
        await using var download = await http.GetStreamAsync(url, token);
        using var buffer = new MemoryStream();
        await download.CopyToAsync(buffer, token);
        buffer.Position = 0;

        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        var csvEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidDataException("В архива няма CSV файл.");
        using var reader = new StreamReader(csvEntry.Open(), Encoding.UTF8);

        var header = ReadRecord(reader) ?? throw new InvalidDataException("Празен CSV файл.");
        var col = header.Select((name, i) => (name.Trim(), i))
            .GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);

        string Get(List<string> r, string name) => col.TryGetValue(name, out var i) && i < r.Count ? r[i].Trim() : string.Empty;

        var maxYear = DateTime.UtcNow.Year + 1;
        var rows = new List<EpaRow>();
        List<string>? record;
        while ((record = ReadRecord(reader)) != null)
        {
            if (!int.TryParse(Get(record, "year"), out var year) || year < minYear || year > maxYear) continue;
            var make = Get(record, "make");
            var model = Get(record, "model");
            if (make.Length == 0 || model.Length == 0) continue;

            var baseModel = Get(record, "baseModel");
            var atv = Get(record, "atvType");
            var fuel = MapFuel(atv, Get(record, "fuelType1"), Get(record, "fuelType"));
            double.TryParse(Get(record, "displ"), NumberStyles.Float, CultureInfo.InvariantCulture, out var displ);
            int.TryParse(Get(record, "cylinders"), out var cylinders);

            rows.Add(new EpaRow(
                Make: make,
                Family: CleanModel(string.IsNullOrEmpty(baseModel) ? model : baseModel),
                Variant: CleanModel(model),
                Year: year,
                Engine: EngineName(fuel, displ, cylinders, Get(record, "tCharger"), Get(record, "sCharger"), Get(record, "evMotor")),
                Fuel: fuel,
                Cc: displ > 0 ? (int)Math.Round(displ * 1000) : null,
                Transmission: MapTransmission(Get(record, "trany")),
                Drive: MapDrive(Get(record, "drive")),
                Body: MapBody(Get(record, "VClass"))));
        }
        return rows;
    }

    /// <summary>Чете един CSV запис (поддържа кавички и запетаи в полетата).</summary>
    private static List<string>? ReadRecord(TextReader reader)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        int c;
        var any = false;
        while ((c = reader.Read()) != -1)
        {
            any = true;
            var ch = (char)c;
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"') { sb.Append('"'); reader.Read(); }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else if (ch == '\n') { fields.Add(sb.ToString()); return fields; }
            else if (ch != '\r') sb.Append(ch);
        }
        if (!any) return null;
        fields.Add(sb.ToString());
        return fields;
    }

    // ---------- 2. Добавяне в каталога ----------

    private async Task MergeAsync(ApplicationDbContext db, List<EpaRow> rows, CatalogImport log, CancellationToken token)
    {
        var brands = await db.CarBrands.ToListAsync(token);
        var brandByName = brands.ToDictionary(b => Key(b.Name), b => b);

        foreach (var makeGroup in rows.GroupBy(r => BrandName(r.Make, brandByName)))
        {
            token.ThrowIfCancellationRequested();

            if (!brandByName.TryGetValue(Key(makeGroup.Key), out var brand))
            {
                brand = new CarBrand { Name = makeGroup.Key };
                db.CarBrands.Add(brand);
                await db.SaveChangesAsync(token);
                brandByName[Key(brand.Name)] = brand;
                log.BrandsAdded++;
            }

            var models = await db.CarModels.Where(m => m.CarBrandId == brand.Id).ToListAsync(token);
            var modelByName = models.GroupBy(m => Key(m.Name)).ToDictionary(g => g.Key, g => g.First());
            var modelIds = models.Select(m => m.Id).ToList();
            var epaGens = await db.CarGenerations
                .Include(g => g.Engines)
                .Where(g => modelIds.Contains(g.CarModelId) && g.Source == CatalogSources.Epa)
                .ToListAsync(token);

            foreach (var familyGroup in makeGroup.GroupBy(r => r.Family, StringComparer.OrdinalIgnoreCase))
            {
                if (!modelByName.TryGetValue(Key(familyGroup.Key), out var model))
                {
                    model = new CarModel { CarBrandId = brand.Id, Name = Cut(familyGroup.Key, 100) };
                    db.CarModels.Add(model);
                    await db.SaveChangesAsync(token);
                    modelByName[Key(model.Name)] = model;
                    log.ModelsAdded++;
                }

                foreach (var variantGroup in familyGroup.GroupBy(r => r.Variant, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Cut(variantGroup.Key, 150);
                    var gen = epaGens.FirstOrDefault(g => g.CarModelId == model.Id && Key(g.Name) == Key(name));
                    var yearFrom = variantGroup.Min(r => r.Year);
                    var yearTo = variantGroup.Max(r => r.Year);
                    if (gen == null)
                    {
                        gen = new CarGeneration
                        {
                            CarModelId = model.Id,
                            Name = name,
                            Source = CatalogSources.Epa,
                            YearFrom = yearFrom,
                            YearTo = yearTo,
                            BodyType = variantGroup.GroupBy(r => r.Body).OrderByDescending(g => g.Count()).First().Key
                        };
                        db.CarGenerations.Add(gen);
                        epaGens.Add(gen);
                        log.GenerationsAdded++;
                    }
                    else
                    {
                        gen.YearFrom = Math.Min(gen.YearFrom, yearFrom);
                        gen.YearTo = Math.Max(gen.YearTo, yearTo);
                    }

                    foreach (var eg in variantGroup.GroupBy(r => (r.Engine, r.Transmission, r.Drive)))
                    {
                        var engine = gen.Engines.FirstOrDefault(e => e.Name == eg.Key.Engine && e.Transmission == eg.Key.Transmission && e.DriveType == eg.Key.Drive);
                        var first = eg.First();
                        if (engine == null)
                        {
                            gen.Engines.Add(new CarEngine
                            {
                                Name = Cut(eg.Key.Engine, 150),
                                FuelType = first.Fuel,
                                DisplacementCc = first.Cc,
                                Transmission = Cut(eg.Key.Transmission, 100),
                                DriveType = Cut(eg.Key.Drive, 50),
                                YearFrom = eg.Min(r => r.Year),
                                YearTo = eg.Max(r => r.Year)
                            });
                            log.EnginesAdded++;
                        }
                        else
                        {
                            engine.YearFrom = Math.Min(engine.YearFrom ?? int.MaxValue, eg.Min(r => r.Year));
                            engine.YearTo = Math.Max(engine.YearTo ?? 0, eg.Max(r => r.Year));
                        }
                    }
                }
            }

            await db.SaveChangesAsync(token);
            db.ChangeTracker.Clear();
            brands = await db.CarBrands.ToListAsync(token);
            brandByName = brands.ToDictionary(b => Key(b.Name), b => b);
        }
    }

    // ---------- 3. Превод на стойностите на български ----------

    private static readonly Dictionary<string, string> MakeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MINI"] = "Mini", ["smart"] = "Smart", ["Mercedes-Benz"] = "Mercedes-Benz", ["Mercedes Benz"] = "Mercedes-Benz",
        ["Mclaren Automotive"] = "McLaren", ["McLaren Automotive"] = "McLaren", ["Aston Martin"] = "Aston Martin",
        ["Rolls-Royce"] = "Rolls-Royce", ["Land Rover"] = "Land Rover", ["Alfa Romeo"] = "Alfa Romeo",
        ["Ram"] = "RAM", ["GMC"] = "GMC", ["BMW"] = "BMW", ["Fiat"] = "Fiat", ["FIAT"] = "Fiat"
    };

    private static string BrandName(string make, Dictionary<string, CarBrand> existing)
    {
        if (existing.TryGetValue(Key(make), out var b)) return b.Name;
        if (MakeNames.TryGetValue(make, out var mapped)) return mapped;
        return make.Length > 3 && make == make.ToUpperInvariant()
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(make.ToLowerInvariant())
            : make;
    }

    private static string CleanModel(string model)
    {
        // "RAV4 AWD", "RAV4 2WD" → "RAV4"
        var cleaned = Regex.Replace(model, @"\b(2WD|4WD|AWD|FWD|RWD|4x4|FFV|Pickup 2WD|Pickup 4WD)\b", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', '/');
        return cleaned.Length == 0 ? model : cleaned;
    }

    private static FuelType MapFuel(string atv, string fuel1, string fuel)
    {
        switch (atv.ToLowerInvariant())
        {
            case "ev": return FuelType.Electric;
            case "hybrid": return FuelType.Hybrid;
            case "plug-in hybrid": return FuelType.PlugInHybrid;
            case "cng":
            case "bifuel (cng)": return FuelType.CNG;
            case "bifuel (lpg)": return FuelType.LPG;
            case "diesel": return FuelType.Diesel;
        }
        var f = (fuel1 + " " + fuel).ToLowerInvariant();
        if (f.Contains("electric")) return FuelType.Electric;
        if (f.Contains("diesel")) return FuelType.Diesel;
        if (f.Contains("natural gas")) return FuelType.CNG;
        if (f.Contains("propane")) return FuelType.LPG;
        return FuelType.Petrol;
    }

    private static string EngineName(FuelType fuel, double displ, int cylinders, string turbo, string super, string evMotor)
    {
        if (fuel == FuelType.Electric || displ <= 0)
        {
            var motor = Regex.Match(evMotor, @"\d+\s*(kW|KW|kw)").Value;
            return "Електрически" + (motor.Length > 0 ? $" ({motor.Replace("KW", "kW").Replace("kw", "kW")})" : "");
        }

        var sb = new StringBuilder(displ.ToString("0.0", CultureInfo.InvariantCulture) + "L");
        if (cylinders > 0) sb.Append($" {cylinders} цил.");
        if (turbo.Equals("T", StringComparison.OrdinalIgnoreCase)) sb.Append(" турбо");
        if (super.Equals("S", StringComparison.OrdinalIgnoreCase)) sb.Append(" компресор");
        sb.Append(fuel switch
        {
            FuelType.Hybrid => " хибрид",
            FuelType.PlugInHybrid => " plug-in хибрид",
            FuelType.Diesel => " дизел",
            FuelType.CNG => " метан",
            FuelType.LPG => " газ",
            _ => " бензин"
        });
        return sb.ToString();
    }

    private static string MapTransmission(string trany)
    {
        if (string.IsNullOrWhiteSpace(trany)) return "Не е посочена";
        var t = trany.ToUpperInvariant();
        var speeds = Regex.Match(t, @"(\d+)(?:-SPD)?\)?$").Groups[1].Value;
        if (speeds.Length == 0) speeds = Regex.Match(t, @"(\d+)").Groups[1].Value;
        var prefix = speeds.Length > 0 && speeds != "1" ? $"{speeds}-степенна " : "";

        if (t.StartsWith("MANUAL")) return (prefix + "ръчна").Trim().Capitalize();
        if (t.Contains("AV") || t.Contains("VARIABLE")) return "Безстепенна (CVT)";
        if (t.Contains("A1)")) return "Едностепенна (електромобил)";
        if (t.Contains("AM")) return (prefix + "автоматизирана (DCT/AMT)").Capitalize();
        return (prefix + "автоматична").Capitalize();
    }

    private static string MapDrive(string drive)
    {
        var d = drive.ToLowerInvariant();
        if (d.Contains("front")) return "Предно";
        if (d.Contains("rear")) return "Задно";
        if (d.Contains("all") || d.Contains("4-wheel") || d.Contains("4wd") || d.Contains("4x4")) return "4x4";
        return "Не е посочено";
    }

    private static BodyType MapBody(string vclass)
    {
        var v = vclass.ToLowerInvariant();
        if (v.Contains("pickup")) return BodyType.Pickup;
        if (v.Contains("sport utility")) return BodyType.Suv;
        if (v.Contains("van")) return BodyType.Van;
        if (v.Contains("wagon")) return BodyType.Wagon;
        if (v.Contains("two seater") || v.Contains("minicompact")) return BodyType.Coupe;
        return BodyType.Sedan;
    }

    private static string Key(string value) => value.Trim().ToLowerInvariant().Replace('-', ' ');
    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}

internal static class StringExtensions
{
    public static string Capitalize(this string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0]) + value[1..];
}

/// <summary>При първо стартиране внася автоматично данните от FuelEconomy.gov (ако е разрешено в appsettings.json).</summary>
public class PublicCatalogImportService : BackgroundService
{
    private readonly PublicCatalogImporter _importer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public PublicCatalogImportService(PublicCatalogImporter importer, IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _importer = importer;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (bool.TryParse(_config["PublicData:AutoImport"], out var auto) && !auto) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // сайтът първо да тръгне
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.CatalogImports.AnyAsync(i => i.Status == "Успешен", stoppingToken)) return;
            }
            await _importer.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // спиране на приложението
        }
    }
}
