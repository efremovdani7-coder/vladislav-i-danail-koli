using CarReliability.Data;
using CarReliability.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.ViewComponents;

/// <summary>Брой непрочетени известия и съобщения в навигацията.</summary>
public class NavBadgesViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NavBadgesViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _userManager.GetUserId(HttpContext.User);
        var model = new NavBadges();
        if (userId != null)
        {
            model.Notifications = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            model.Messages = await _context.ChatMessages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);
        }
        return View(model);
    }
}

public class NavBadges
{
    public int Notifications { get; set; }
    public int Messages { get; set; }
}
