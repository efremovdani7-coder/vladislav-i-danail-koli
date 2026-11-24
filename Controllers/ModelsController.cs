using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>Страница на модел: индекс на надеждност (US07), графики (US04), поколения (US03),
/// оценки и коментари (US10), „Следи модел“ (US11), линкове към обяви (US16).</summary>
public class ModelsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReliabilityService _reliability;

    public ModelsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ReliabilityService reliability)
    {
        _context = context;
        _userManager = userManager;
        _reliability = reliability;
    }

    public async Task<IActionResult> Details(int id)
    {
        var model = await _context.CarModels
            .Include(m => m.CarBrand)
            .Include(m => m.Generations).ThenInclude(g => g.Engines)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);
        if (model == null) return NotFound();

        var points = await _reliability.EntryPoints().Where(e => e.ModelId == id).ToListAsync();
        var stars = await _context.ModelRatings.Where(r => r.CarModelId == id).Select(r => r.Stars).ToListAsync();
        var userId = _userManager.GetUserId(User);

        var vm = new ModelDetailsViewModel
        {
            Model = model,
            Stats = ReliabilityService.Compute(points, stars),
            Entries = await _context.CarEntries
                .Include(e => e.CarGeneration).Include(e => e.CarEngine).Include(e => e.User)
                .Where(e => e.CarModelId == id)
                .OrderByDescending(e => e.CreatedAt).Take(20)
                .ToListAsync(),
            Comments = await _context.ModelComments.Include(c => c.User)
                .Where(c => c.CarModelId == id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(),
            FollowersCount = await _context.ModelFollows.CountAsync(f => f.CarModelId == id)
        };

        if (userId != null)
        {
            vm.MyStars = await _context.ModelRatings.Where(r => r.CarModelId == id && r.UserId == userId)
                .Select(r => (int?)r.Stars).FirstOrDefaultAsync();
            vm.IsFollowing = await _context.ModelFollows.AnyAsync(f => f.CarModelId == id && f.UserId == userId);
        }

        // US04: разходи и оценка по година на производство
        var byYear = points.GroupBy(p => p.Year).OrderBy(g => g.Key).ToList();
        foreach (var g in byYear)
        {
            vm.RatingByYear.Labels.Add(g.Key.ToString());
            vm.RatingByYear.Values.Add(Math.Round(g.Average(p => p.Rating), 1));
            vm.RatingByYear.Counts.Add(g.Count());

            var costs = g.Where(p => p.Cost.HasValue).Select(p => (double)p.Cost!.Value).ToList();
            if (costs.Count > 0)
            {
                vm.CostByYear.Labels.Add(g.Key.ToString());
                vm.CostByYear.Values.Add(Math.Round(costs.Average()));
                vm.CostByYear.Counts.Add(costs.Count);
            }
        }

        // US04: разходи според пробега (на групи по 50 000 км)
        foreach (var g in points.Where(p => p.Cost.HasValue).GroupBy(p => Math.Min(p.Mileage / 50000, 8)).OrderBy(g => g.Key))
        {
            var from = g.Key * 50;
            vm.CostByMileage.Labels.Add(g.Key == 8 ? "400+ хил. км" : $"{from}–{from + 50} хил. км");
            vm.CostByMileage.Values.Add(Math.Round(g.Average(p => (double)p.Cost!.Value)));
            vm.CostByMileage.Counts.Add(g.Count());
        }

        // US03: сравнение на поколенията (старо срещу ново)
        vm.GenerationStats = model.Generations
            .Where(g => points.Any(p => p.GenerationId == g.Id))
            .OrderBy(g => g.YearFrom)
            .Select(g => new GenerationStat
            {
                GenerationId = g.Id,
                Name = g.Name,
                YearFrom = g.YearFrom,
                YearTo = g.YearTo,
                Stats = ReliabilityService.Compute(points.Where(p => p.GenerationId == g.Id).ToList(), Array.Empty<int>())
            })
            .ToList();

        return View(vm);
    }

    // US10: оценка 1–5 звезди (една на потребител, може да се промени)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Rate(int id, int stars)
    {
        if (stars < 1 || stars > 5 || !await _context.CarModels.AnyAsync(m => m.Id == id)) return BadRequest();
        var userId = _userManager.GetUserId(User)!;

        var rating = await _context.ModelRatings.FirstOrDefaultAsync(r => r.CarModelId == id && r.UserId == userId);
        if (rating == null)
            _context.ModelRatings.Add(new ModelRating { CarModelId = id, UserId = userId, Stars = stars });
        else
        {
            rating.Stars = stars;
            rating.CreatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Благодарим за оценката ({stars} от 5)!";
        return RedirectToAction(nameof(Details), new { id, tab = "comments" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> AddComment(int id, string text)
    {
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > 1000)
        {
            TempData["Error"] = "Коментарът трябва да е между 1 и 1000 символа.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await _context.CarModels.AnyAsync(m => m.Id == id)) return NotFound();

        _context.ModelComments.Add(new ModelComment { CarModelId = id, UserId = _userManager.GetUserId(User)!, Text = text });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Коментарът е публикуван.";
        return Redirect(Url.Action(nameof(Details), new { id }) + "#comments");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int commentId)
    {
        var comment = await _context.ModelComments.FindAsync(commentId);
        if (comment == null) return NotFound();
        if (comment.UserId != _userManager.GetUserId(User) && !User.IsInRole("Admin")) return Forbid();

        _context.ModelComments.Remove(comment);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Коментарът е изтрит.";
        return Redirect(Url.Action(nameof(Details), new { id = comment.CarModelId }) + "#comments");
    }

    // US11: следене на модел
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Follow(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        if (await _context.CarModels.AnyAsync(m => m.Id == id) &&
            !await _context.ModelFollows.AnyAsync(f => f.CarModelId == id && f.UserId == userId))
        {
            _context.ModelFollows.Add(new ModelFollow { CarModelId = id, UserId = userId });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Следите този модел. Ще получавате известие и имейл при нови данни.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Unfollow(int id, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User)!;
        var follow = await _context.ModelFollows.FirstOrDefaultAsync(f => f.CarModelId == id && f.UserId == userId);
        if (follow != null)
        {
            _context.ModelFollows.Remove(follow);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Вече не следите този модел.";
        }
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }
}
