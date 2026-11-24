using CarReliability.Data;
using CarReliability.Models;
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
        return Ok(gens.Select(g => new { id = g.Id, name = g.DisplayName, from = g.YearFrom, to = g.YearTo }));
    }

    [HttpGet("years")]
    public async Task<IActionResult> Years(int generationId)
    {
        var g = await _context.CarGenerations.FindAsync(generationId);
        if (g == null) return Ok(Array.Empty<int>());
        return Ok(YearsOf(g));
    }

    [HttpGet("engines")]
    public async Task<IActionResult> Engines(int generationId) =>
        Ok(await EngineOptionsAsync(_context, generationId));

    [HttpGet("variants")]
    public async Task<IActionResult> Variants(int generationId, string engine) =>
        Ok(await VariantOptionsAsync(_context, generationId, engine));

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

    public static async Task<List<EngineOption>> EngineOptionsAsync(ApplicationDbContext context, int generationId)
    {
        var engines = await context.CarEngines
            .Where(e => e.CarGenerationId == generationId)
            .Select(e => new { e.Name, e.FuelType, e.PowerHp, e.DisplacementCc })
            .ToListAsync();

        return engines
            .GroupBy(e => e.Name)
            .Select(g => g.First())
            .OrderBy(e => e.FuelType).ThenBy(e => e.DisplacementCc ?? 0).ThenBy(e => e.PowerHp ?? 0)
            .Select(e => new EngineOption(e.Name, $"{e.Name} — {EnumText.Fuel(e.FuelType)}"))
            .ToList();
    }

    public static async Task<List<VariantOption>> VariantOptionsAsync(ApplicationDbContext context, int generationId, string engine)
    {
        var variants = await context.CarEngines
            .Where(e => e.CarGenerationId == generationId && e.Name == engine)
            .OrderBy(e => e.Transmission).ThenBy(e => e.DriveType)
            .ToListAsync();
        return variants.Select(v => new VariantOption(v.Id, VariantText(v))).ToList();
    }

    public static string VariantText(CarEngine v) =>
        v.DriveType.StartsWith("Не е") ? $"{v.Transmission} · задвижване: не е посочено" : $"{v.Transmission} · {v.DriveType} задвижване";

    public record EngineOption(string id, string name);
    public record VariantOption(int id, string name);
}
