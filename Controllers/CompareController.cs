using System.Globalization;
using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>US03: сравнение на две коли (конкретно поколение на модел) — размери в реален мащаб
/// (като carsized.com), надеждност и стари срещу нови автомобили.</summary>
public class CompareController : Controller
{
    private static readonly CultureInfo Bg = CultureInfo.GetCultureInfo("bg-BG");
    private readonly ApplicationDbContext _context;
    private readonly ReliabilityService _reliability;

    public CompareController(ApplicationDbContext context, ReliabilityService reliability)
    {
        _context = context;
        _reliability = reliability;
    }

    public async Task<IActionResult> Index(int? a, int? b, int? brandA, int? modelA, int? brandB, int? modelB,
        int? decadeBrandId, int?[]? modelIds)
    {
        // стари линкове ?modelIds=5 → най-новото поколение с размери
        if (!a.HasValue && modelIds?.FirstOrDefault() is int legacyModel)
            a = await LatestGenerationAsync(legacyModel);

        var vm = new CompareViewModel
        {
            Brands = await _context.CarBrands.OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(),
            DecadeBrandId = decadeBrandId
        };

        var points = await _reliability.EntryPoints().ToListAsync();
        vm.A = await BuildSlotAsync(a, brandA, modelA);
        vm.B = await BuildSlotAsync(b, brandB, modelB);
        if (vm.A.GenerationId.HasValue) vm.CarA = await BuildCarAsync(vm.A.GenerationId.Value, points);
        if (vm.B.GenerationId.HasValue) vm.CarB = await BuildCarAsync(vm.B.GenerationId.Value, points);
        if (vm.CarA != null && vm.CarB != null) vm.Summary = Summarize(vm.CarA, vm.CarB);

        // Стари срещу нови: индекс по десетилетие на производство
        var decadePoints = decadeBrandId.HasValue ? points.Where(p => p.BrandId == decadeBrandId.Value).ToList() : points;
        if (decadeBrandId.HasValue)
            vm.ByDecadeTitle = vm.Brands.FirstOrDefault(x => x.Value == decadeBrandId.Value.ToString())?.Text ?? vm.ByDecadeTitle;
        foreach (var g in decadePoints.GroupBy(p => p.Year / 10 * 10).OrderBy(g => g.Key))
        {
            vm.ByDecade.Labels.Add($"{g.Key}-те");
            vm.ByDecade.Values.Add(ReliabilityService.Compute(g.ToList(), Array.Empty<int>()).Index);
            vm.ByDecade.Counts.Add(g.Count());
        }

        return View(vm);
    }

    private async Task<int?> LatestGenerationAsync(int modelId) =>
        await _context.CarGenerations.Where(g => g.CarModelId == modelId)
            .OrderByDescending(g => g.LengthMm != null).ThenByDescending(g => g.YearTo).ThenBy(g => g.Name)
            .Select(g => (int?)g.Id).FirstOrDefaultAsync();

    private async Task<CompareSlot> BuildSlotAsync(int? generationId, int? brandId, int? modelId)
    {
        var slot = new CompareSlot { BrandId = brandId, ModelId = modelId };
        if (generationId.HasValue)
        {
            var gen = await _context.CarGenerations.Include(g => g.CarModel).FirstOrDefaultAsync(g => g.Id == generationId.Value);
            if (gen != null)
            {
                slot.GenerationId = gen.Id;
                slot.ModelId = gen.CarModelId;
                slot.BrandId = gen.CarModel!.CarBrandId;
            }
        }
        if (slot.ModelId.HasValue && !slot.BrandId.HasValue)
            slot.BrandId = await _context.CarModels.Where(m => m.Id == slot.ModelId).Select(m => (int?)m.CarBrandId).FirstOrDefaultAsync();

        if (slot.BrandId.HasValue)
            slot.Models = await _context.CarModels.Where(m => m.CarBrandId == slot.BrandId).OrderBy(m => m.Name)
                .Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToListAsync();
        if (slot.ModelId.HasValue)
        {
            var gens = await _context.CarGenerations.Where(g => g.CarModelId == slot.ModelId)
                .OrderBy(g => g.YearFrom).ThenBy(g => g.Name).ToListAsync();
            slot.Generations = gens.Select(g => new SelectListItem(g.DisplayName + " · " + EnumText.Body(g.BodyType), g.Id.ToString())).ToList();
        }
        return slot;
    }

    private async Task<CompareCar?> BuildCarAsync(int generationId, List<EntryPoint> points)
    {
        var gen = await _context.CarGenerations
            .Include(g => g.CarModel).ThenInclude(m => m!.CarBrand)
            .Include(g => g.Engines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == generationId);
        if (gen == null) return null;

        var car = new CompareCar
        {
            Generation = gen,
            Sizes = await SizesOfAsync(gen),
            EngineCount = gen.Engines.Select(e => e.Name).Distinct().Count(),
            MinPowerHp = gen.Engines.Where(e => e.PowerHp.HasValue).Select(e => e.PowerHp).DefaultIfEmpty().Min(),
            MaxPowerHp = gen.Engines.Where(e => e.PowerHp.HasValue).Select(e => e.PowerHp).DefaultIfEmpty().Max(),
            Fuels = gen.Engines.Select(e => e.FuelType).Distinct().OrderBy(f => f).Select(EnumText.Fuel).ToList()
        };

        var modelStars = await _context.ModelRatings.Where(r => r.CarModelId == gen.CarModelId).Select(r => r.Stars).ToListAsync();
        car.ModelStats = ReliabilityService.Compute(points.Where(p => p.ModelId == gen.CarModelId).ToList(), modelStars);
        car.GenerationStats = ReliabilityService.Compute(points.Where(p => p.GenerationId == gen.Id).ToList(), Array.Empty<int>());
        return car;
    }

    /// <summary>Ако поколението няма размери (напр. версиите от EPA), ги взимаме от най-близкото поколение на същия модел.</summary>
    private async Task<CarSizes> SizesOfAsync(CarGeneration gen)
    {
        var source = gen;
        if (!gen.HasSizes)
        {
            var siblings = await _context.CarGenerations
                .Where(g => g.CarModelId == gen.CarModelId && g.Id != gen.Id && g.LengthMm != null && g.HeightMm != null)
                .ToListAsync();
            var donor = siblings
                .OrderBy(g => g.BodyType == gen.BodyType ? 0 : 1)
                .ThenBy(g => g.YearFrom <= gen.YearTo && g.YearTo >= gen.YearFrom ? 0 : 1)
                .ThenBy(g => Math.Abs(g.YearTo - gen.YearTo))
                .FirstOrDefault();
            if (donor != null) source = donor;
        }

        return new CarSizes
        {
            Length = source.LengthMm,
            Width = source.WidthMm,
            Height = source.HeightMm,
            Wheelbase = source.WheelbaseMm,
            Clearance = source.GroundClearanceMm,
            Cargo = source.CargoLiters,
            Weight = source.WeightKg,
            Approximate = source != gen || gen.SizesApproximate,
            TakenFrom = source != gen ? source.DisplayName : null
        };
    }

    /// <summary>Обобщение на разликите с думи, напр. „Колата A е с 25,1 см по-къса ... от B“.</summary>
    private static List<string> Summarize(CompareCar a, CompareCar b)
    {
        var list = new List<string>();
        void Mm(int? x, int? y, string more, string less)
        {
            if (!x.HasValue || !y.HasValue || x == y) return;
            var cm = Math.Abs(x.Value - y.Value) / 10.0;
            list.Add($"с {cm.ToString("0.#", Bg)} см {(x > y ? more : less)}");
        }

        Mm(a.Sizes.Length, b.Sizes.Length, "по-дълга", "по-къса");
        Mm(a.Sizes.Height, b.Sizes.Height, "по-висока", "по-ниска");
        Mm(a.Sizes.Width, b.Sizes.Width, "по-широка", "по-тясна");
        Mm(a.Sizes.Wheelbase, b.Sizes.Wheelbase, "по-дълго междуосие", "по-късо междуосие");
        Mm(a.Sizes.Clearance, b.Sizes.Clearance, "по-голям клиренс", "по-малък клиренс");
        if (a.Sizes.Cargo is int ca && b.Sizes.Cargo is int cb && cb > 0 && ca != cb)
        {
            var pct = Math.Abs(ca - cb) * 100.0 / cb;
            list.Add($"с {pct.ToString("0", Bg)}% {(ca > cb ? "повече" : "по-малко")} място в багажника");
        }
        if (a.Sizes.Weight is int wa && b.Sizes.Weight is int wb && wa != wb)
            list.Add($"с {Math.Abs(wa - wb)} кг {(wa > wb ? "по-тежка" : "по-лека")}");
        if (a.GenerationStats.HasData && b.GenerationStats.HasData && a.GenerationStats.Index != b.GenerationStats.Index)
            list.Add($"с {Math.Abs(a.GenerationStats.Index - b.GenerationStats.Index)} точки {(a.GenerationStats.Index > b.GenerationStats.Index ? "по-висок" : "по-нисък")} индекс на надеждност");
        return list;
    }
}
