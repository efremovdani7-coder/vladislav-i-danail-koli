using CarReliability.Data;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>US03: сравнение „рамо до рамо“ на 2–3 модела и сравнение на стари срещу нови автомобили.</summary>
public class CompareController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReliabilityService _reliability;

    public CompareController(ApplicationDbContext context, ReliabilityService reliability)
    {
        _context = context;
        _reliability = reliability;
    }

    public async Task<IActionResult> Index(int?[]? brandIds, int?[]? modelIds, int splitYear = 2010, int? decadeBrandId = null)
    {
        brandIds ??= Array.Empty<int?>();
        modelIds ??= Array.Empty<int?>();

        var vm = new CompareViewModel
        {
            SplitYear = Math.Clamp(splitYear, 1990, DateTime.Now.Year),
            Brands = await _context.CarBrands.OrderBy(b => b.Name)
                .Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToListAsync()
        };

        var allPoints = await _reliability.EntryPoints().ToListAsync();

        for (var i = 0; i < 3; i++)
        {
            var slot = new CompareSlot
            {
                BrandId = brandIds.ElementAtOrDefault(i),
                ModelId = modelIds.ElementAtOrDefault(i)
            };

            // ако е избран само модел (напр. от линк), намираме марката му
            if (slot.ModelId.HasValue && !slot.BrandId.HasValue)
                slot.BrandId = await _context.CarModels.Where(m => m.Id == slot.ModelId).Select(m => (int?)m.CarBrandId).FirstOrDefaultAsync();

            if (slot.BrandId.HasValue)
                slot.Models = await _context.CarModels.Where(m => m.CarBrandId == slot.BrandId).OrderBy(m => m.Name)
                    .Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToListAsync();
            vm.Slots.Add(slot);

            if (!slot.ModelId.HasValue || vm.Results.Any(r => r.Model.Id == slot.ModelId)) continue;

            var model = await _context.CarModels.Include(m => m.CarBrand).Include(m => m.Generations)
                .FirstOrDefaultAsync(m => m.Id == slot.ModelId);
            if (model == null) continue;

            var points = allPoints.Where(p => p.ModelId == model.Id).ToList();
            var stars = await _context.ModelRatings.Where(r => r.CarModelId == model.Id).Select(r => r.Stars).ToListAsync();

            vm.Results.Add(new CompareResult
            {
                Model = model,
                Stats = ReliabilityService.Compute(points, stars),
                OldStats = ReliabilityService.Compute(points.Where(p => p.Year < vm.SplitYear).ToList(), Array.Empty<int>()),
                NewStats = ReliabilityService.Compute(points.Where(p => p.Year >= vm.SplitYear).ToList(), Array.Empty<int>()),
                YearFrom = model.Generations.Count > 0 ? model.Generations.Min(g => g.YearFrom) : null,
                YearTo = model.Generations.Count > 0 ? model.Generations.Max(g => g.YearTo) : null
            });
        }

        // Средна оценка по десетилетия на производство
        var decadePoints = allPoints;
        if (decadeBrandId.HasValue)
        {
            decadePoints = allPoints.Where(p => p.BrandId == decadeBrandId.Value).ToList();
            vm.ByDecadeTitle = vm.Brands.FirstOrDefault(b => b.Value == decadeBrandId.Value.ToString())?.Text ?? vm.ByDecadeTitle;
        }
        foreach (var g in decadePoints.GroupBy(p => p.Year / 10 * 10).OrderBy(g => g.Key))
        {
            var stats = ReliabilityService.Compute(g.ToList(), Array.Empty<int>());
            vm.ByDecade.Labels.Add($"{g.Key}-те");
            vm.ByDecade.Values.Add(stats.Index);
            vm.ByDecade.Counts.Add(g.Count());
        }
        ViewBag.DecadeBrandId = decadeBrandId;

        return View(vm);
    }
}
