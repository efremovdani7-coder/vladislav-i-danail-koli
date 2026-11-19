using CarReliability.Models;

namespace CarReliability.ViewModels;

public class HomeViewModel
{
    public int TotalCars { get; set; }
    public int TotalUsers { get; set; }
    public int TotalBrands { get; set; }
    public double AverageRating { get; set; }
    public List<CarEntry> LatestEntries { get; set; } = new();
    public List<BrandStat> TopBrands { get; set; } = new();
}

public class BrandStat
{
    public string BrandName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgRating { get; set; }
}
