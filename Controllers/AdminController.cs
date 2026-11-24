using CarReliability.Data;
using CarReliability.Models;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Admin
    public async Task<IActionResult> Index(string? searchTerm, string? filterUser)
    {
        var query = _context.CarEntries
            .Include(c => c.CarBrand)
            .Include(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(c =>
                c.CarBrand!.Name.Contains(searchTerm) ||
                c.Model.Contains(searchTerm) ||
                c.User!.Nickname.Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(filterUser))
        {
            query = query.Where(c => c.UserId == filterUser);
        }

        var vm = new AdminDashboardViewModel
        {
            AllEntries = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(),
            AllUsers = await _context.Users.Cast<ApplicationUser>().ToListAsync(),
            TotalEntries = await _context.CarEntries.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            SearchTerm = searchTerm,
            FilterUser = filterUser
        };

        return View(vm);
    }

    // GET: Admin/EditEntry/5
    public async Task<IActionResult> EditEntry(int? id)
    {
        if (id == null) return NotFound();

        var entry = await _context.CarEntries
            .Include(c => c.CarBrand)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entry == null) return NotFound();

        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", entry.CarBrandId);
        return View(entry);
    }

    // POST: Admin/EditEntry/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEntry(int id, CarEntry entry)
    {
        if (id != entry.Id) return NotFound();

        var existing = await _context.CarEntries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null) return NotFound();

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
            TempData["Success"] = "Записът е обновен от администратора.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Brands = new SelectList(
            await _context.CarBrands.OrderBy(b => b.Name).ToListAsync(), "Id", "Name", entry.CarBrandId);
        return View(entry);
    }

    // POST: Admin/DeleteEntry/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(int id)
    {
        var entry = await _context.CarEntries.FindAsync(id);
        if (entry == null) return NotFound();

        _context.CarEntries.Remove(entry);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Записът е изтрит от администратора.";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Users
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users.Cast<ApplicationUser>().ToListAsync();
        var userList = new List<(ApplicationUser User, IList<string> Roles, int EntriesCount)>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var entriesCount = await _context.CarEntries.CountAsync(c => c.UserId == user.Id);
            userList.Add((user, roles, entriesCount));
        }

        return View(userList);
    }

    // POST: Admin/DeleteUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Don't allow deleting yourself
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "Не можете да изтриете собствения си акаунт!";
            return RedirectToAction(nameof(Users));
        }

        await _userManager.DeleteAsync(user);
        TempData["Success"] = $"Потребител {user.Nickname} е изтрит.";
        return RedirectToAction(nameof(Users));
    }

    // POST: Admin/BlockUser (US06)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (user.Id == _userManager.GetUserId(User) || await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Администратори не могат да бъдат блокирани.";
            return RedirectToAction(nameof(Users));
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        // Invalidate the user's login cookie so they are signed out
        await _userManager.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"Потребител {user.Nickname} е блокиран.";
        return RedirectToAction(nameof(Users));
    }

    // POST: Admin/UnblockUser (US06)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        TempData["Success"] = $"Потребител {user.Nickname} е отблокиран.";
        return RedirectToAction(nameof(Users));
    }

    // GET: Admin/Brands
    public async Task<IActionResult> Brands()
    {
        var brands = await _context.CarBrands
            .Include(b => b.CarEntries)
            .OrderBy(b => b.Name)
            .ToListAsync();
        return View(brands);
    }

    // POST: Admin/AddBrand
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBrand(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Името на марката не може да е празно.";
            return RedirectToAction(nameof(Brands));
        }

        if (await _context.CarBrands.AnyAsync(b => b.Name == name))
        {
            TempData["Error"] = "Тази марка вече съществува.";
            return RedirectToAction(nameof(Brands));
        }

        _context.CarBrands.Add(new CarBrand { Name = name });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Марката '{name}' е добавена.";
        return RedirectToAction(nameof(Brands));
    }
}
