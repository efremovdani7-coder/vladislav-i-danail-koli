using CarReliability.Data;
using CarReliability.Models;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Services;

/// <summary>Данни от един отзив, нужни за изчисленията.</summary>
public class EntryPoint
{
    public int ModelId { get; set; }
    public int BrandId { get; set; }
    public int GenerationId { get; set; }
    public int Year { get; set; }
    public int Mileage { get; set; }
    public decimal? Cost { get; set; }
    public int Rating { get; set; }
    public DefectCategory Defects { get; set; }
}

public class ReliabilityStats
{
    /// <summary>Индекс на надеждност 1–100 (0 = няма данни).</summary>
    public int Index { get; set; }
    public int Entries { get; set; }
    public double AvgRating { get; set; }
    public decimal? AvgCost { get; set; }
    public int? AvgMileage { get; set; }
    public double AvgDefects { get; set; }
    public double? AvgStars { get; set; }
    public int StarVotes { get; set; }
    public List<(string Name, int Count)> DefectCounts { get; set; } = new();

    public bool HasData => Entries > 0 || StarVotes > 0;

    public string Level => Index >= 80 ? "Висока" : Index >= 50 ? "Средна" : Index > 0 ? "Ниска" : "Няма данни";
    public string CssClass => Index >= 80 ? "success" : Index >= 50 ? "warning" : Index > 0 ? "danger" : "secondary";
}

public class BrandReliability
{
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public ReliabilityStats Stats { get; set; } = new();
}

public class ModelReliability
{
    public int ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public ReliabilityStats Stats { get; set; } = new();
}

/// <summary>
/// US07: Индекс на надеждност (1–100), изчислен от:
///  • средната оценка на собствениците (1–10) — 50%
///  • честотата на дефектите — 20%
///  • годишните разходи за поддръжка — 15%
///  • звездите от потребителите (1–5) — 15%
/// Изчислява се отделно за всеки модел и за всяка марка.
/// </summary>
public class ReliabilityService
{
    private readonly ApplicationDbContext _context;

    public ReliabilityService(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQueryable<EntryPoint> EntryPoints() => _context.CarEntries.Select(e => new EntryPoint
    {
        ModelId = e.CarModelId,
        BrandId = e.CarModel!.CarBrandId,
        GenerationId = e.CarGenerationId,
        Year = e.Year,
        Mileage = e.Mileage,
        Cost = e.MaintenanceCost,
        Rating = e.ReliabilityRating,
        Defects = e.Defects
    });

    public static ReliabilityStats Compute(IReadOnlyCollection<EntryPoint> entries, IReadOnlyCollection<int> stars)
    {
        var stats = new ReliabilityStats { Entries = entries.Count, StarVotes = stars.Count };
        if (!stats.HasData) return stats;

        double weighted = 0, weights = 0;

        if (entries.Count > 0)
        {
            stats.AvgRating = Math.Round(entries.Average(e => e.Rating), 1);
            stats.AvgMileage = (int)entries.Average(e => e.Mileage);
            stats.AvgDefects = Math.Round(entries.Average(e => EnumText.DefectCount(e.Defects)), 2);

            var costs = entries.Where(e => e.Cost.HasValue).Select(e => e.Cost!.Value).ToList();
            if (costs.Count > 0) stats.AvgCost = Math.Round(costs.Average(), 0);

            stats.DefectCounts = EnumText.Defects
                .Select(d => (d.Name, entries.Count(e => e.Defects.HasFlag(d.Value))))
                .Where(d => d.Item2 > 0)
                .OrderByDescending(d => d.Item2)
                .ToList();

            weighted += stats.AvgRating * 10 * 0.50; weights += 0.50;
            weighted += Math.Max(0, 100 - stats.AvgDefects * 20) * 0.20; weights += 0.20;
            if (stats.AvgCost.HasValue)
            {
                weighted += Math.Max(0, 100 - (double)stats.AvgCost.Value / 30) * 0.15; weights += 0.15;
            }
        }

        if (stars.Count > 0)
        {
            stats.AvgStars = Math.Round(stars.Average(), 1);
            weighted += (stats.AvgStars.Value - 1) / 4 * 100 * 0.15; weights += 0.15;
        }

        stats.Index = (int)Math.Clamp(Math.Round(weighted / weights), 1, 100);
        return stats;
    }

    public async Task<ReliabilityStats> ForModelAsync(int modelId)
    {
        var entries = await EntryPoints().Where(e => e.ModelId == modelId).ToListAsync();
        var stars = await _context.ModelRatings.Where(r => r.CarModelId == modelId).Select(r => r.Stars).ToListAsync();
        return Compute(entries, stars);
    }

    public async Task<ReliabilityStats> ForBrandAsync(int brandId)
    {
        var entries = await EntryPoints().Where(e => e.BrandId == brandId).ToListAsync();
        var stars = await _context.ModelRatings.Where(r => r.CarModel!.CarBrandId == brandId).Select(r => r.Stars).ToListAsync();
        return Compute(entries, stars);
    }

    /// <summary>Обща оценка за всяка марка (а не една обща оценка за всички коли).</summary>
    public async Task<List<BrandReliability>> BrandRankingAsync()
    {
        var entries = await EntryPoints().ToListAsync();
        var stars = await _context.ModelRatings
            .Select(r => new { BrandId = r.CarModel!.CarBrandId, r.Stars })
            .ToListAsync();

        var brandIds = entries.Select(e => e.BrandId).Concat(stars.Select(s => s.BrandId)).Distinct().ToList();
        var names = await _context.CarBrands.Where(b => brandIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name);

        return brandIds
            .Select(id => new BrandReliability
            {
                BrandId = id,
                BrandName = names.GetValueOrDefault(id, "?"),
                Stats = Compute(entries.Where(e => e.BrandId == id).ToList(),
                                stars.Where(s => s.BrandId == id).Select(s => s.Stars).ToList())
            })
            .OrderByDescending(b => b.Stats.Index)
            .ThenByDescending(b => b.Stats.Entries)
            .ToList();
    }

    public async Task<List<ModelReliability>> ModelRankingAsync(int? brandId = null)
    {
        var query = EntryPoints();
        if (brandId.HasValue) query = query.Where(e => e.BrandId == brandId.Value);
        var entries = await query.ToListAsync();

        var ratingQuery = _context.ModelRatings.AsQueryable();
        if (brandId.HasValue) ratingQuery = ratingQuery.Where(r => r.CarModel!.CarBrandId == brandId.Value);
        var stars = await ratingQuery.Select(r => new { r.CarModelId, r.Stars }).ToListAsync();

        var modelIds = entries.Select(e => e.ModelId).Concat(stars.Select(s => s.CarModelId)).Distinct().ToList();
        var models = await _context.CarModels.Include(m => m.CarBrand)
            .Where(m => modelIds.Contains(m.Id)).ToListAsync();

        return models
            .Select(m => new ModelReliability
            {
                ModelId = m.Id,
                ModelName = m.Name,
                BrandName = m.CarBrand?.Name ?? "",
                Stats = Compute(entries.Where(e => e.ModelId == m.Id).ToList(),
                                stars.Where(s => s.CarModelId == m.Id).Select(s => s.Stars).ToList())
            })
            .OrderByDescending(m => m.Stats.Index)
            .ThenByDescending(m => m.Stats.Entries)
            .ToList();
    }
}
