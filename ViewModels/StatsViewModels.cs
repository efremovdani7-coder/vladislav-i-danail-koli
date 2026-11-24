using CarReliability.Models;
using CarReliability.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CarReliability.ViewModels;

public class HomeViewModel
{
    public int TotalEntries { get; set; }
    public int TotalUsers { get; set; }
    public int CatalogBrands { get; set; }
    public int CatalogModels { get; set; }
    public List<BrandReliability> BrandRanking { get; set; } = new();
    public List<ModelReliability> TopModels { get; set; } = new();
    public List<CarEntry> LatestEntries { get; set; } = new();
}

public class ChartSeries
{
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
    public List<int> Counts { get; set; } = new();
    public bool HasData => Values.Count > 0;
}

public class GenerationStat
{
    public int GenerationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
    public ReliabilityStats Stats { get; set; } = new();
}

public class ModelDetailsViewModel
{
    public CarModel Model { get; set; } = null!;
    public ReliabilityStats Stats { get; set; } = new();
    public List<CarEntry> Entries { get; set; } = new();
    public List<ModelComment> Comments { get; set; } = new();
    public int? MyStars { get; set; }
    public bool IsFollowing { get; set; }
    public int FollowersCount { get; set; }

    // US04: графики
    public ChartSeries CostByYear { get; set; } = new();
    public ChartSeries CostByMileage { get; set; } = new();
    public ChartSeries RatingByYear { get; set; } = new();

    // US03: старо срещу ново (по поколения)
    public List<GenerationStat> GenerationStats { get; set; } = new();
}

public class BrandIndexViewModel
{
    public List<BrandReliability> Ranked { get; set; } = new();
    public List<(int Id, string Name, int Models)> AllBrands { get; set; } = new();
}

public class BrandDetailsViewModel
{
    public CarBrand Brand { get; set; } = null!;
    public ReliabilityStats Stats { get; set; } = new();
    public int? Rank { get; set; }
    public int RankedBrands { get; set; }
    public List<ModelReliability> RankedModels { get; set; } = new();
    public List<CarModel> AllModels { get; set; } = new();
}

public class CompareSlot
{
    public int? BrandId { get; set; }
    public int? ModelId { get; set; }
    public List<SelectListItem> Models { get; set; } = new();
}

public class CompareResult
{
    public CarModel Model { get; set; } = null!;
    public ReliabilityStats Stats { get; set; } = new();
    public ReliabilityStats OldStats { get; set; } = new();
    public ReliabilityStats NewStats { get; set; } = new();
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}

public class CompareViewModel
{
    public List<CompareSlot> Slots { get; set; } = new();
    public List<SelectListItem> Brands { get; set; } = new();
    public int SplitYear { get; set; } = 2010;
    public List<CompareResult> Results { get; set; } = new();

    /// <summary>Средна оценка по десетилетия на производство (по-старите по-здрави ли са?).</summary>
    public ChartSeries ByDecade { get; set; } = new();
    public string ByDecadeTitle { get; set; } = "всички марки";
}
