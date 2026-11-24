using CarReliability.Data;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>Обща оценка (индекс на надеждност) за всяка марка.</summary>
public class BrandsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReliabilityService _reliability;

    public BrandsController(ApplicationDbContext context, ReliabilityService reliability)
    {
        _context = context;
        _reliability = reliability;
    }

    public async Task<IActionResult> Index()
    {
        var all = await _context.CarBrands
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, Models = b.Models.Count })
            .ToListAsync();

        return View(new BrandIndexViewModel
        {
            Ranked = await _reliability.BrandRankingAsync(),
            AllBrands = all.Select(b => (b.Id, b.Name, b.Models)).ToList()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var brand = await _context.CarBrands.FindAsync(id);
        if (brand == null) return NotFound();

        var ranking = await _reliability.BrandRankingAsync();
        var position = ranking.FindIndex(b => b.BrandId == id);

        return View(new BrandDetailsViewModel
        {
            Brand = brand,
            Stats = await _reliability.ForBrandAsync(id),
            Rank = position >= 0 ? position + 1 : null,
            RankedBrands = ranking.Count,
            RankedModels = await _reliability.ModelRankingAsync(id),
            AllModels = await _context.CarModels.Where(m => m.CarBrandId == id).OrderBy(m => m.Name).ToListAsync()
        });
    }
}
