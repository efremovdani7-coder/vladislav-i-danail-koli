using CarReliability.Data;
using CarReliability.Models;
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

    public CarsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Cars — Browse all entries
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 12;
        var query = _context.CarEntries
            .Include(c => c.CarBrand)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt);

        var total = await query.CountAsync();
        var entries = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(entries);
    }

    // GET: Cars/Search
    public async Task<IActionResult> Search(CarSearchViewModel vm)
    {
        vm.AvailableBrands = await _context.CarBrands.OrderBy(b => b.Name).ToListAsync();

        var query = _context.CarEntries
            .Include(c => c.CarBrand)
            .Include(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(vm.Brand))
            query = query.Where(c => c.CarBrand!.Name.Contains(vm.Brand));

        if (!string.IsNullOrEmpty(vm.Model))
            query = query.Where(c => c.Model.Contains(vm.Model));

        if (vm.YearFrom.HasValue)
            query = query.Where(c => c.Year >= vm.YearFrom.Value);

        if (vm.YearTo.HasValue)
            query = query.Where(c => c.Year <= vm.YearTo.Value);

        if (vm.EngineType.HasValue)
            query = query.Where(c => c.EngineType == vm.EngineType.Value);

        if (vm.MinRating.HasValue)
            query = query.Where(c => c.ReliabilityRating >= vm.MinRating.Value);

        var total = await query.CountAsync();
        vm.TotalPages = (int)Math.Ceiling(total / (double)vm.PageSize);

        vm.Results = await query
            .OrderByDescending(c => c.ReliabilityRating)
            .Skip((vm.CurrentPage - 1) * vm.PageSize)
            .Take(vm.PageSize)
            .ToListAsync();

        return View(vm);
    }

    // GET: Cars/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var entry = await _context.CarEntries
            .Include(c => c.CarBrand)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entry == null) return NotFound();

        return View(entry);
    }

    // GET: Cars/Create
    [Authorize]
    public async Task<IActionResult> Create()
    {
        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name");
        return View();
    }

    // POST: Cars/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Create(CarEntry entry)
    {
        entry.UserId = _userManager.GetUserId(User)!;
        entry.CreatedAt = DateTime.UtcNow;

        // Remove navigation properties from validation
        ModelState.Remove("UserId");
        ModelState.Remove("User");
        ModelState.Remove("CarBrand");

        if (ModelState.IsValid)
        {
            _context.CarEntries.Add(entry);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Автомобилът е добавен успешно!";
            return RedirectToAction(nameof(Details), new { id = entry.Id });
        }

        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", entry.CarBrandId);
        return View(entry);
    }

    // GET: Cars/Edit/5
    [Authorize]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var entry = await _context.CarEntries.FindAsync(id);
        if (entry == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        if (entry.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", entry.CarBrandId);
        return View(entry);
    }

    // POST: Cars/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(int id, CarEntry entry)
    {
        if (id != entry.Id) return NotFound();

        var existing = await _context.CarEntries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        if (existing.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        entry.UserId = existing.UserId;
        entry.CreatedAt = existing.CreatedAt;
        entry.UpdatedAt = DateTime.UtcNow;

        ModelState.Remove("UserId");
        ModelState.Remove("User");
        ModelState.Remove("CarBrand");

        if (ModelState.IsValid)
        {
            _context.Update(entry);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Данните са обновени успешно!";
            return RedirectToAction(nameof(Details), new { id = entry.Id });
        }

        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", entry.CarBrandId);
        return View(entry);
    }

    // POST: Cars/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _context.CarEntries.FindAsync(id);
        if (entry == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        if (entry.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        _context.CarEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Записът е изтрит успешно!";
        return RedirectToAction(nameof(Index));
    }
}
