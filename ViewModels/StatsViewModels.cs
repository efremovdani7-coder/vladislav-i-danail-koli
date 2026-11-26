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
    public int? GenerationId { get; set; }
    public List<SelectListItem> Models { get; set; } = new();
    public List<SelectListItem> Generations { get; set; } = new();
}

/// <summary>Размерите, които се показват (може да са взети от съседно поколение).</summary>
public class CarSizes
{
    public int? Length { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Wheelbase { get; set; }
    public int? Clearance { get; set; }
    public int? Cargo { get; set; }
    public int? Weight { get; set; }
    public bool Approximate { get; set; }
    public string? TakenFrom { get; set; }
    public bool HasMain => Length.HasValue && Height.HasValue;
}

public class CompareCar
{
    public CarGeneration Generation { get; set; } = null!;
    public string Title => $"{Generation.CarModel?.CarBrand?.Name} {Generation.Name}";
    public CarSizes Sizes { get; set; } = new();
    public ReliabilityStats GenerationStats { get; set; } = new();
    public ReliabilityStats ModelStats { get; set; } = new();
    public int? MinPowerHp { get; set; }
    public int? MaxPowerHp { get; set; }
    public int EngineCount { get; set; }
    public List<string> Fuels { get; set; } = new();
}

public class CompareViewModel
{
    public CompareSlot A { get; set; } = new();
    public CompareSlot B { get; set; } = new();
    public List<SelectListItem> Brands { get; set; } = new();
    public CompareCar? CarA { get; set; }
    public CompareCar? CarB { get; set; }

    /// <summary>Текстово обобщение на разликите (като в carsized.com).</summary>
    public List<string> Summary { get; set; } = new();

    /// <summary>Индекс на надеждност по десетилетия на производство (по-старите по-здрави ли са?).</summary>
    public ChartSeries ByDecade { get; set; } = new();
    public string ByDecadeTitle { get; set; } = "всички марки";
    public int? DecadeBrandId { get; set; }
}
