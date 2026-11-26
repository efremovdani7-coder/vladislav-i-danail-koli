using System.Text;
using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>US06: администраторски панел — модерация на данни, коментари, потребители и история на влизанията.</summary>
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

    // ---------- Отзиви ----------

    public async Task<IActionResult> Index(string? searchTerm, string? filterUser)
    {
        var query = _context.CarEntries
            .Include(e => e.CarModel).ThenInclude(m => m!.CarBrand)
            .Include(e => e.CarEngine)
            .Include(e => e.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var t = searchTerm.Trim().ToLower();
            query = query.Where(e =>
                (e.CarModel!.CarBrand!.Name + " " + e.CarModel.Name).ToLower().Contains(t) ||
                e.User!.Nickname.ToLower().Contains(t));
        }
        if (!string.IsNullOrEmpty(filterUser))
            query = query.Where(e => e.UserId == filterUser);

        var today = DateTime.UtcNow.Date;
        return View(new AdminDashboardViewModel
        {
            Entries = await query.OrderByDescending(e => e.CreatedAt).Take(200).ToListAsync(),
            AllUsers = await _context.Users.OrderBy(u => u.Nickname).ToListAsync(),
            TotalEntries = await _context.CarEntries.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            TotalComments = await _context.ModelComments.CountAsync(),
            LoginsToday = await _context.LoginRecords.CountAsync(l => l.LoggedAt >= today && l.Succeeded),
            SearchTerm = searchTerm,
            FilterUser = filterUser
        });
    }

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

    // ---------- Коментари ----------

    public async Task<IActionResult> Comments()
    {
        var comments = await _context.ModelComments
            .Include(c => c.User)
            .Include(c => c.CarModel).ThenInclude(m => m!.CarBrand)
            .OrderByDescending(c => c.CreatedAt)
            .Take(300)
            .ToListAsync();
        return View(comments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await _context.ModelComments.FindAsync(id);
        if (comment != null)
        {
            _context.ModelComments.Remove(comment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Коментарът е изтрит.";
        }
        return RedirectToAction(nameof(Comments));
    }

    // ---------- Потребители ----------

    public async Task<IActionResult> Users()
    {
        var users = await _context.Users.OrderBy(u => u.Nickname).ToListAsync();
        var entryCounts = await _context.CarEntries.GroupBy(e => e.UserId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var lastLogins = await _context.LoginRecords.Where(l => l.Succeeded && l.UserId != null)
            .GroupBy(l => l.UserId!)
            .Select(g => new { g.Key, Last = g.Max(l => l.LoggedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last);

        var rows = new List<AdminUserRow>();
        foreach (var u in users)
        {
            rows.Add(new AdminUserRow
            {
                User = u,
                Roles = await _userManager.GetRolesAsync(u),
                EntriesCount = entryCounts.GetValueOrDefault(u.Id),
                LastLogin = lastLogins.TryGetValue(u.Id, out var last) ? last : null,
                IsBlocked = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow
            });
        }
        return View(rows);
    }

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
        await _userManager.UpdateSecurityStampAsync(user); // изхвърля потребителя от сайта
        TempData["Success"] = $"Потребител {user.Nickname} е блокиран.";
        return RedirectToAction(nameof(Users));
    }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        if (user.Id == _userManager.GetUserId(User) || await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Не можете да изтриете администратор.";
            return RedirectToAction(nameof(Users));
        }
        await _userManager.DeleteAsync(user);
        TempData["Success"] = $"Потребител {user.Nickname} е изтрит заедно с данните си.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Каталог, публични бази и тестови данни ----------

    public async Task<IActionResult> Catalog([FromServices] PublicCatalogImporter importer, [FromServices] DatabaseSettings database)
    {
        return View(new AdminCatalogViewModel
        {
            Brands = await _context.CarBrands.CountAsync(),
            Models = await _context.CarModels.CountAsync(),
            AutoEvolutionGenerations = await _context.CarGenerations.CountAsync(g => g.Source == CatalogSources.AutoEvolution),
            EpaGenerations = await _context.CarGenerations.CountAsync(g => g.Source == CatalogSources.Epa),
            AutoEvolutionEngines = await _context.CarEngines.CountAsync(e => e.CarGeneration!.Source == CatalogSources.AutoEvolution),
            EpaEngines = await _context.CarEngines.CountAsync(e => e.CarGeneration!.Source == CatalogSources.Epa),
            GenerationsWithSizes = await _context.CarGenerations.CountAsync(g => g.LengthMm != null),
            Imports = await _context.CatalogImports.OrderByDescending(i => i.StartedAt).Take(20).ToListAsync(),
            ImportRunning = importer.IsRunning,
            TestUsers = await _context.Users.CountAsync(u => u.IsTestAccount),
            TestEntries = await _context.CarEntries.CountAsync(e => e.User!.IsTestAccount),
            DatabaseDescription = database.Description,
            DatabaseWarning = database.Warning
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportPublicData([FromServices] PublicCatalogImporter importer)
    {
        TempData[importer.StartInBackground() ? "Success" : "Error"] = importer.IsRunning
            ? "Импортът от FuelEconomy.gov започна във фонов режим. Обновете страницата след 1–2 минути."
            : "Импортът вече работи.";
        return RedirectToAction(nameof(Catalog));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTestData([FromServices] TestDataSeeder seeder)
    {
        var added = await seeder.SeedAsync();
        TempData[added > 0 ? "Success" : "Error"] = added > 0 ? $"Добавени са тестови данни ({added} отзива)." : "Тестовите данни вече са добавени.";
        return RedirectToAction(nameof(Catalog));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTestData([FromServices] TestDataSeeder seeder)
    {
        var removed = await seeder.RemoveAsync();
        TempData["Success"] = $"Изтрити са {removed} тестови потребители заедно с техните отзиви, коментари и съобщения.";
        return RedirectToAction(nameof(Catalog));
    }

    // ---------- История на влизанията ----------

    public async Task<IActionResult> Logins(string? search, string? status, DateTime? from, DateTime? to, int page = 1)
    {
        const int pageSize = 50;
        var query = FilterLogins(search, status, from, to);

        var vm = new LoginHistoryViewModel
        {
            Search = search,
            Status = status,
            From = from,
            To = to,
            TotalRecords = await query.CountAsync(),
            SuccessCount = await query.CountAsync(l => l.Succeeded),
            FailedCount = await query.CountAsync(l => !l.Succeeded)
        };
        vm.TotalPages = Math.Max(1, (int)Math.Ceiling(vm.TotalRecords / (double)pageSize));
        vm.CurrentPage = Math.Clamp(page, 1, vm.TotalPages);
        vm.Records = await query.OrderByDescending(l => l.LoggedAt)
            .Skip((vm.CurrentPage - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLogin(int id)
    {
        var record = await _context.LoginRecords.FindAsync(id);
        if (record != null)
        {
            _context.LoginRecords.Remove(record);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Записът е изтрит.";
        }
        return RedirectToAction(nameof(Logins));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearLogins(int olderThanDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, olderThanDays));
        var old = await _context.LoginRecords.Where(l => l.LoggedAt < cutoff).ToListAsync();
        _context.LoginRecords.RemoveRange(old);
        await _context.SaveChangesAsync();
        TempData["Success"] = olderThanDays == 0
            ? $"Изтрити са всички {old.Count} записа."
            : $"Изтрити са {old.Count} записа, по-стари от {olderThanDays} дни.";
        return RedirectToAction(nameof(Logins));
    }

    public async Task<IActionResult> ExportLogins(string? search, string? status, DateTime? from, DateTime? to)
    {
        var records = await FilterLogins(search, status, from, to).OrderByDescending(l => l.LoggedAt).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Дата и час;Имейл;Никнейм;Начин;Успешен;Причина;IP адрес;Браузър");
        foreach (var r in records)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                r.LoggedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), r.Login, r.Nickname ?? "", r.Method,
                r.Succeeded ? "Да" : "Не", r.FailureReason ?? "", r.IpAddress ?? "", r.UserAgent ?? ""
            }.Select(v => "\"" + v.Replace("\"", "\"\"") + "\"")));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"vlizaniya-{DateTime.Now:yyyy-MM-dd}.csv");
    }

    private IQueryable<LoginRecord> FilterLogins(string? search, string? status, DateTime? from, DateTime? to)
    {
        var query = _context.LoginRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            query = query.Where(l => l.Login.ToLower().Contains(t) || (l.Nickname != null && l.Nickname.ToLower().Contains(t))
                                     || (l.IpAddress != null && l.IpAddress.Contains(t)));
        }
        if (status == "ok") query = query.Where(l => l.Succeeded);
        if (status == "failed") query = query.Where(l => !l.Succeeded);
        if (from.HasValue) query = query.Where(l => l.LoggedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(l => l.LoggedAt < to.Value.AddDays(1).ToUniversalTime());
        return query;
    }
}
