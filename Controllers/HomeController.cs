using CarReliability.Data;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReliabilityService _reliability;

    public HomeController(ApplicationDbContext context, ReliabilityService reliability)
    {
        _context = context;
        _reliability = reliability;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            TotalEntries = await _context.CarEntries.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            CatalogBrands = await _context.CarBrands.CountAsync(),
            CatalogModels = await _context.CarModels.CountAsync(),
            // обща оценка за всяка марка поотделно
            BrandRanking = (await _reliability.BrandRankingAsync()).Take(10).ToList(),
            TopModels = (await _reliability.ModelRankingAsync()).Where(m => m.Stats.Entries > 0).Take(6).ToList(),
            LatestEntries = await _context.CarEntries
                .Include(e => e.CarModel).ThenInclude(m => m!.CarBrand)
                .Include(e => e.CarEngine)
                .Include(e => e.User)
                .OrderByDescending(e => e.CreatedAt)
                .Take(6)
                .ToListAsync()
        };
        return View(vm);
    }

    public IActionResult Error() => View();
}
