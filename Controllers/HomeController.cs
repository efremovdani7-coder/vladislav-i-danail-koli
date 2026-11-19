using CarReliability.Data;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            TotalCars = await _context.CarEntries.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            TotalBrands = await _context.CarBrands.CountAsync(),
            AverageRating = await _context.CarEntries.AnyAsync()
                ? Math.Round(await _context.CarEntries.AverageAsync(c => c.ReliabilityRating), 1)
                : 0,
            LatestEntries = await _context.CarEntries
                .Include(c => c.CarBrand)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .ToListAsync(),
            TopBrands = await _context.CarEntries
                .Include(c => c.CarBrand)
                .GroupBy(c => c.CarBrand!.Name)
                .Select(g => new BrandStat
                {
                    BrandName = g.Key,
                    Count = g.Count(),
                    AvgRating = Math.Round(g.Average(c => c.ReliabilityRating), 1)
                })
                .OrderByDescending(b => b.AvgRating)
                .Take(5)
                .ToListAsync()
        };

        return View(vm);
    }

    public IActionResult Error()
    {
        return View();
    }
}
