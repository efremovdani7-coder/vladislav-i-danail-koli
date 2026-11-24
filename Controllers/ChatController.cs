using CarReliability.Data;
using CarReliability.Models;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

/// <summary>US13: лични съобщения между потребители (съобщенията се изпращат през ChatHub).</summary>
[Authorize]
public class ChatController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? userId)
    {
        var me = _userManager.GetUserId(User)!;

        var myMessages = await _context.ChatMessages
            .Where(m => m.SenderId == me || m.ReceiverId == me)
            .Select(m => new { m.SenderId, m.ReceiverId, m.Text, m.SentAt, m.IsRead })
            .ToListAsync();

        var partnerIds = myMessages.Select(m => m.SenderId == me ? m.ReceiverId : m.SenderId).Distinct().ToList();
        var nicknames = await _context.Users.Where(u => partnerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nickname);

        var vm = new ChatViewModel
        {
            MyId = me,
            Conversations = partnerIds
                .Where(nicknames.ContainsKey)
                .Select(id =>
                {
                    var last = myMessages.Where(m => m.SenderId == id || m.ReceiverId == id).OrderByDescending(m => m.SentAt).First();
                    return new ConversationItem
                    {
                        UserId = id,
                        Nickname = nicknames[id],
                        LastText = last.Text,
                        LastAt = last.SentAt,
                        Unread = myMessages.Count(m => m.SenderId == id && !m.IsRead)
                    };
                })
                .OrderByDescending(c => c.LastAt)
                .ToList(),
            OtherUsers = await _context.Users.Where(u => u.Id != me).OrderBy(u => u.Nickname).ToListAsync()
        };

        if (!string.IsNullOrEmpty(userId) && userId != me)
        {
            vm.Partner = await _userManager.FindByIdAsync(userId);
            if (vm.Partner != null)
            {
                vm.Messages = await _context.ChatMessages
                    .Where(m => (m.SenderId == me && m.ReceiverId == userId) || (m.SenderId == userId && m.ReceiverId == me))
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                var unread = vm.Messages.Where(m => m.ReceiverId == me && !m.IsRead).ToList();
                if (unread.Count > 0)
                {
                    unread.ForEach(m => m.IsRead = true);
                    await _context.SaveChangesAsync();
                    var conv = vm.Conversations.FirstOrDefault(c => c.UserId == userId);
                    if (conv != null) conv.Unread = 0;
                }
            }
        }

        return View(vm);
    }
}
