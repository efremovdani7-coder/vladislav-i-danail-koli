using CarReliability.Models;

namespace CarReliability.ViewModels;

public class AdminDashboardViewModel
{
    public List<CarEntry> AllEntries { get; set; } = new();
    public List<ApplicationUser> AllUsers { get; set; } = new();
    public int TotalEntries { get; set; }
    public int TotalUsers { get; set; }
    public string? SearchTerm { get; set; }
    public string? FilterUser { get; set; }
}
