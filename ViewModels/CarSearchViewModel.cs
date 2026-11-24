using CarReliability.Models;

namespace CarReliability.ViewModels;

public class CarSearchViewModel
{
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public EngineType? EngineType { get; set; }
    public int? MinRating { get; set; }

    public List<CarEntry> Results { get; set; } = new();
    public List<CarBrand> AvailableBrands { get; set; } = new();
    public List<string> AvailableModels { get; set; } = new();

    public int TotalResults { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 10;
}
