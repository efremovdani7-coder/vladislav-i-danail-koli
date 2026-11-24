using CarReliability.Data;
using CarReliability.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Hubs;

/// <summary>US13: чат в реално време (SignalR / WebSockets). Използва се и за известия на живо.</summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task SendMessage(string receiverId, string text)
    {
        var senderId = Context.UserIdentifier;
        if (senderId == null) return;

        text = (text ?? string.Empty).Trim();
        if (text.Length == 0) return;
        if (text.Length > 1000) text = text[..1000];
        if (receiverId == senderId) return;

        var sender = await _userManager.FindByIdAsync(senderId);
        var receiverExists = await _context.Users.AnyAsync(u => u.Id == receiverId);
        if (sender == null || !receiverExists) return;

        var message = new ChatMessage { SenderId = senderId, ReceiverId = receiverId, Text = text, SentAt = DateTime.UtcNow };
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        var payload = new
        {
            id = message.Id,
            senderId,
            receiverId,
            senderNickname = sender.Nickname,
            text = message.Text,
            sentAt = message.SentAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
        };

        await Clients.User(receiverId).SendAsync("receiveMessage", payload);
        await Clients.Caller.SendAsync("messageSent", payload);
    }

    public async Task MarkRead(string otherUserId)
    {
        var me = Context.UserIdentifier;
        if (me == null) return;

        var unread = await _context.ChatMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == me && !m.IsRead)
            .ToListAsync();
        if (unread.Count == 0) return;

        unread.ForEach(m => m.IsRead = true);
        await _context.SaveChangesAsync();
    }
}
