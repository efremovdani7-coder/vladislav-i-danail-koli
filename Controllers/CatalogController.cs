using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>JSON API за каскадните падащи менюта (Марка → Модел → Поколение → Година → Двигател → Кутия)
/// и за автокомплита на търсачката (US09).</summary>
[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CatalogController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models(int brandId) =>
        Ok(await _context.CarModels
            .Where(m => m.CarBrandId == brandId)
            .OrderBy(m => m.Name)
            .Select(m => new { id = m.Id, name = m.Name })
            .ToListAsync());

    [HttpGet("generations")]
    public async Task<IActionResult> Generations(int modelId)
    {
        var gens = await _context.CarGenerations
            .Where(g => g.CarModelId == modelId)
            .OrderBy(g => g.YearFrom).ThenBy(g => g.Name)
            .ToListAsync();
        return Ok(gens.Select(g => new { id = g.Id, name = g.DisplayName + " · " + EnumText.Body(g.BodyType), from = g.YearFrom, to = g.YearTo }));
    }

    [HttpGet("years")]
    public async Task<IActionResult> Years(int generationId)
    {
        var g = await _context.CarGenerations.FindAsync(generationId);
        if (g == null) return Ok(Array.Empty<int>());
        return Ok(YearsOf(g));
    }

    [HttpGet("engines")]
    public async Task<IActionResult> Engines(int generationId, int? year) =>
        Ok(await EngineOptionsAsync(_context, generationId, year));

    [HttpGet("variants")]
    public async Task<IActionResult> Variants(int generationId, string engine, int? year) =>
        Ok(await VariantOptionsAsync(_context, generationId, engine, year));

    /// <summary>Подробности за поколение (за сравнението на размерите).</summary>
    [HttpGet("generation/{id:int}")]
    public async Task<IActionResult> Generation(int id)
    {
        var g = await _context.CarGenerations.Include(x => x.CarModel).ThenInclude(m => m!.CarBrand).FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) return NotFound();
        return Ok(new
        {
            id = g.Id, name = g.DisplayName, brand = g.CarModel?.CarBrand?.Name, model = g.CarModel?.Name,
            body = g.BodyType.ToString(), length = g.LengthMm, width = g.WidthMm, height = g.HeightMm,
            wheelbase = g.WheelbaseMm, clearance = g.GroundClearanceMm, cargo = g.CargoLiters, weight = g.WeightKg,
            approximate = g.SizesApproximate
        });
    }

    /// <summary>Разчитане на VIN номер чрез публичната база NHTSA vPIC.</summary>
    [HttpGet("vin")]
    public async Task<IActionResult> Vin(string vin, [FromServices] VinDecoder decoder) =>
        Ok(await decoder.DecodeAsync(vin, HttpContext.RequestAborted));

    /// <summary>US09: подсказки при писане (от 2 символа нагоре).</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest(string? q)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2) return Ok(Array.Empty<object>());
        var lower = q.ToLower();

        var brands = await _context.CarBrands
            .Where(b => b.Name.ToLower().Contains(lower))
            .OrderBy(b => b.Name)
            .Take(5)
            .Select(b => new { type = "brand", label = b.Name, url = "/Brands/Details/" + b.Id })
            .ToListAsync();

        // търсене и по "Марка Модел", напр. "toyota cor"
        var models = await _context.CarModels
            .Where(m => (m.CarBrand!.Name + " " + m.Name).ToLower().Contains(lower) || m.Name.ToLower().Contains(lower))
            .OrderBy(m => m.CarBrand!.Name).ThenBy(m => m.Name)
            .Take(10)
            .Select(m => new { type = "model", label = m.CarBrand!.Name + " " + m.Name, url = "/Models/Details/" + m.Id })
            .ToListAsync();

        return Ok(brands.Concat(models));
    }

    // ---- помощни методи, използвани и от CarsController ----

    public static IEnumerable<int> YearsOf(CarGeneration g)
    {
        var to = Math.Max(g.YearFrom, Math.Min(g.YearTo, DateTime.Now.Year));
        return Enumerable.Range(g.YearFrom, to - g.YearFrom + 1).Reverse();
    }

    public static async Task<List<EngineOption>> EngineOptionsAsync(ApplicationDbContext context, int generationId, int? year = null)
    {
        var engines = await ForYear(context.CarEngines.Where(e => e.CarGenerationId == generationId), year)
            .Select(e => new { e.Name, e.FuelType, e.PowerHp, e.DisplacementCc })
            .ToListAsync();

        return engines
            .GroupBy(e => e.Name)
            .Select(g => g.First())
            .OrderBy(e => e.FuelType).ThenBy(e => e.DisplacementCc ?? 0).ThenBy(e => e.PowerHp ?? 0)
            .Select(e => new EngineOption(e.Name, $"{e.Name} — {EnumText.Fuel(e.FuelType)}"))
            .ToList();
    }

    public static async Task<List<VariantOption>> VariantOptionsAsync(ApplicationDbContext context, int generationId, string engine, int? year = null)
    {
        var variants = await ForYear(context.CarEngines.Where(e => e.CarGenerationId == generationId && e.Name == engine), year)
            .OrderBy(e => e.Transmission).ThenBy(e => e.DriveType)
            .ToListAsync();
        return variants.Select(v => new VariantOption(v.Id, VariantText(v))).ToList();
    }

    /// <summary>Само двигателите, предлагани през избраната година.</summary>
    public static IQueryable<CarEngine> ForYear(IQueryable<CarEngine> query, int? year) =>
        year.HasValue
            ? query.Where(e => (e.YearFrom == null || e.YearFrom <= year.Value) && (e.YearTo == null || e.YearTo >= year.Value))
            : query;

    public static string VariantText(CarEngine v) =>
        v.DriveType.StartsWith("Не е") ? $"{v.Transmission} · задвижване: не е посочено" : $"{v.Transmission} · {v.DriveType} задвижване";

    public record EngineOption(string id, string name);
    public record VariantOption(int id, string name);
}
