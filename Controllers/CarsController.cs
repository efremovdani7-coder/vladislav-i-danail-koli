using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

public class CarsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReliabilityService _reliability;
    private readonly NotificationService _notifications;

    public CarsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
        ReliabilityService reliability, NotificationService notifications)
    {
        _context = context;
        _userManager = userManager;
        _reliability = reliability;
        _notifications = notifications;
    }

    private IQueryable<CarEntry> EntriesWithDetails() => _context.CarEntries
        .Include(e => e.CarModel).ThenInclude(m => m!.CarBrand)
        .Include(e => e.CarGeneration)
        .Include(e => e.CarEngine)
        .Include(e => e.User);

    // GET: /Cars — всички отзиви
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 12;
        var total = await _context.CarEntries.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var entries = await EntriesWithDetails()
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return View(new CarListViewModel { Entries = entries, CurrentPage = page, TotalPages = totalPages });
    }

    // GET: /Cars/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var entry = await EntriesWithDetails().FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound();
        return View(entry);
    }

    // GET: /Cars/Search — US02
    public async Task<IActionResult> Search(CarSearchViewModel vm)
    {
        vm.Brands = await BrandItemsAsync();
        if (vm.BrandId.HasValue)
            vm.Models = await ModelItemsAsync(vm.BrandId.Value);
        if (vm.CurrentPage < 1) vm.CurrentPage = 1;

        var query = EntriesWithDetails().AsQueryable();
        if (vm.BrandId.HasValue) query = query.Where(e => e.CarModel!.CarBrandId == vm.BrandId.Value);
        if (vm.ModelId.HasValue) query = query.Where(e => e.CarModelId == vm.ModelId.Value);
        if (vm.YearFrom.HasValue) query = query.Where(e => e.Year >= vm.YearFrom.Value);
        if (vm.YearTo.HasValue) query = query.Where(e => e.Year <= vm.YearTo.Value);
        if (vm.Fuel.HasValue) query = query.Where(e => e.CarEngine!.FuelType == vm.Fuel.Value);
        if (vm.MinRating.HasValue) query = query.Where(e => e.ReliabilityRating >= vm.MinRating.Value);

        vm.TotalResults = await query.CountAsync();
        vm.TotalPages = (int)Math.Ceiling(vm.TotalResults / (double)vm.PageSize);
        vm.Results = await query
            .OrderByDescending(e => e.ReliabilityRating).ThenByDescending(e => e.CreatedAt)
            .Skip((vm.CurrentPage - 1) * vm.PageSize).Take(vm.PageSize)
            .ToListAsync();

        if (vm.ModelId.HasValue)
        {
            var model = await _context.CarModels.Include(m => m.CarBrand).FirstOrDefaultAsync(m => m.Id == vm.ModelId.Value);
            if (model != null)
            {
                vm.SummaryTitle = model.FullName;
                vm.SummaryModelId = model.Id;
                vm.Summary = await _reliability.ForModelAsync(model.Id);
            }
        }
        else if (vm.BrandId.HasValue)
        {
            var brand = await _context.CarBrands.FindAsync(vm.BrandId.Value);
            if (brand != null)
            {
                vm.SummaryTitle = brand.Name;
                vm.SummaryBrandId = brand.Id;
                vm.Summary = await _reliability.ForBrandAsync(brand.Id);
            }
        }

        // Заявките от JavaScript получават само резултатите (без презареждане на страницата)
        if (Request.Headers["X-Requested-With"] == "fetch")
            return PartialView("_SearchResults", vm);

        return View(vm);
    }

    // GET: /Cars/Create?modelId=5 — US01
    [Authorize]
    public async Task<IActionResult> Create(int? modelId)
    {
        var vm = new CarEntryFormViewModel();
        if (modelId.HasValue)
        {
            var model = await _context.CarModels.FindAsync(modelId.Value);
            if (model != null)
            {
                vm.BrandId = model.CarBrandId;
                vm.ModelId = model.Id;
            }
        }
        await FillOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Create(CarEntryFormViewModel vm)
    {
        var engine = await ValidateCatalogAsync(vm);
        if (!ModelState.IsValid || engine == null)
        {
            await FillOptionsAsync(vm);
            return View(vm);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var entry = new CarEntry { UserId = user.Id, CreatedAt = DateTime.UtcNow };
        Apply(vm, entry, engine);
        _context.CarEntries.Add(entry);
        await _context.SaveChangesAsync();

        // US11: известяване на последователите на модела
        var modelName = $"{engine.CarGeneration!.CarModel!.CarBrand!.Name} {engine.CarGeneration.CarModel.Name}";
        var url = Url.Action("Details", "Models", new { id = entry.CarModelId }) ?? "/";
        var absoluteUrl = Url.Action("Details", "Models", new { id = entry.CarModelId }, Request.Scheme) ?? url;
        await _notifications.NotifyFollowersAsync(entry.CarModelId, user.Id, modelName, user.Nickname, url, absoluteUrl);

        TempData["Success"] = "Отзивът е добавен успешно!";
        return RedirectToAction(nameof(Details), new { id = entry.Id });
    }

    // GET: /Cars/Edit/5
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _context.CarEntries
            .Include(e => e.CarModel)
            .Include(e => e.CarEngine)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound();
        if (!CanModify(entry)) return Forbid();

        var vm = new CarEntryFormViewModel
        {
            Id = entry.Id,
            BrandId = entry.CarModel!.CarBrandId,
            ModelId = entry.CarModelId,
            GenerationId = entry.CarGenerationId,
            Year = entry.Year,
            EngineName = entry.CarEngine!.Name,
            CarEngineId = entry.CarEngineId,
            Mileage = entry.Mileage,
            MaintenanceCost = entry.MaintenanceCost,
            ReliabilityRating = entry.ReliabilityRating,
            SelectedDefects = EnumText.Defects.Where(d => entry.Defects.HasFlag(d.Value)).Select(d => (int)d.Value).ToList(),
            CommonIssues = entry.CommonIssues,
            Pros = entry.Pros,
            Cons = entry.Cons,
            Comment = entry.Comment
        };
        await FillOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(int id, CarEntryFormViewModel vm)
    {
        var entry = await _context.CarEntries.FindAsync(id);
        if (entry == null) return NotFound();
        if (!CanModify(entry)) return Forbid();

        vm.Id = id;
        var engine = await ValidateCatalogAsync(vm);
        if (!ModelState.IsValid || engine == null)
        {
            await FillOptionsAsync(vm);
            return View(vm);
        }

        Apply(vm, entry, engine);
        entry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Данните са обновени успешно!";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var entry = await _context.CarEntries.FindAsync(id);
        if (entry == null) return NotFound();
        if (!CanModify(entry)) return Forbid();

        _context.CarEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Записът е изтрит.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // ---------- помощни методи ----------

    private bool CanModify(CarEntry entry) =>
        entry.UserId == _userManager.GetUserId(User) || User.IsInRole("Admin");

    /// <summary>Проверява, че избраните стойности си съответстват (двигателят е от избраното поколение и т.н.).</summary>
    private async Task<CarEngine?> ValidateCatalogAsync(CarEntryFormViewModel vm)
    {
        if (vm.CarEngineId is not > 0) return null;

        var engine = await _context.CarEngines
            .Include(e => e.CarGeneration).ThenInclude(g => g!.CarModel).ThenInclude(m => m!.CarBrand)
            .FirstOrDefaultAsync(e => e.Id == vm.CarEngineId);

        if (engine == null || engine.CarGenerationId != vm.GenerationId || engine.CarGeneration!.CarModelId != vm.ModelId
            || engine.CarGeneration.CarModel!.CarBrandId != vm.BrandId || engine.Name != vm.EngineName)
        {
            ModelState.AddModelError(nameof(vm.CarEngineId), "Избраният двигател не отговаря на марката, модела и поколението.");
            return null;
        }

        if (vm.Year < engine.CarGeneration.YearFrom || vm.Year > Math.Max(engine.CarGeneration.YearFrom, engine.CarGeneration.YearTo))
            ModelState.AddModelError(nameof(vm.Year), "Годината е извън периода на производство на това поколение.");

        return engine;
    }

    private static void Apply(CarEntryFormViewModel vm, CarEntry entry, CarEngine engine)
    {
        entry.CarEngineId = engine.Id;
        entry.CarGenerationId = engine.CarGenerationId;
        entry.CarModelId = engine.CarGeneration!.CarModelId;
        entry.Year = vm.Year ?? engine.CarGeneration.YearFrom;
        entry.Mileage = vm.Mileage ?? 0;
        entry.MaintenanceCost = vm.MaintenanceCost;
        entry.ReliabilityRating = vm.ReliabilityRating ?? 1;
        entry.Defects = vm.SelectedDefects.Aggregate(DefectCategory.None, (acc, d) => acc | (DefectCategory)d);
        entry.CommonIssues = vm.CommonIssues?.Trim();
        entry.Pros = vm.Pros?.Trim();
        entry.Cons = vm.Cons?.Trim();
        entry.Comment = vm.Comment?.Trim();
    }

    /// <summary>Попълва падащите менюта според вече избраните стойности.</summary>
    private async Task FillOptionsAsync(CarEntryFormViewModel vm)
    {
        vm.Brands = await BrandItemsAsync();
        if (vm.BrandId > 0) vm.Models = await ModelItemsAsync(vm.BrandId.Value);

        if (vm.ModelId > 0)
        {
            var gens = await _context.CarGenerations.Where(g => g.CarModelId == vm.ModelId)
                .OrderBy(g => g.YearFrom).ThenBy(g => g.Name).ToListAsync();
            vm.Generations = gens.Select(g => new SelectListItem(g.DisplayName, g.Id.ToString())).ToList();
        }

        if (vm.GenerationId > 0)
        {
            var gen = await _context.CarGenerations.FindAsync(vm.GenerationId.Value);
            if (gen != null)
                vm.Years = CatalogController.YearsOf(gen).Select(y => new SelectListItem(y.ToString(), y.ToString())).ToList();

            vm.Engines = (await CatalogController.EngineOptionsAsync(_context, vm.GenerationId.Value))
                .Select(e => new SelectListItem(e.name, e.id)).ToList();

            if (!string.IsNullOrEmpty(vm.EngineName))
                vm.Variants = (await CatalogController.VariantOptionsAsync(_context, vm.GenerationId.Value, vm.EngineName))
                    .Select(v => new SelectListItem(v.name, v.id.ToString())).ToList();
        }
    }

    private async Task<List<SelectListItem>> BrandItemsAsync() =>
        await _context.CarBrands.OrderBy(b => b.Name)
            .Select(b => new SelectListItem(b.Name, b.Id.ToString())).ToListAsync();

    private async Task<List<SelectListItem>> ModelItemsAsync(int brandId) =>
        await _context.CarModels.Where(m => m.CarBrandId == brandId).OrderBy(m => m.Name)
            .Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToListAsync();
}
