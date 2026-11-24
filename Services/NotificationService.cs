using System.Net;
using CarReliability.Data;
using CarReliability.Hubs;
using CarReliability.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Services;

/// <summary>US11: известия (в профила + имейл) за последователите на даден модел.</summary>
public class NotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly EmailQueue _emailQueue;
    private readonly IHubContext<ChatHub> _hub;

    public NotificationService(ApplicationDbContext context, EmailQueue emailQueue, IHubContext<ChatHub> hub)
    {
        _context = context;
        _emailQueue = emailQueue;
        _hub = hub;
    }

    public async Task NotifyFollowersAsync(int modelId, string authorId, string modelName, string authorNickname, string url, string absoluteUrl)
    {
        var followers = await _context.ModelFollows
            .Where(f => f.CarModelId == modelId && f.UserId != authorId)
            .Select(f => new { f.UserId, f.User!.Email, f.User.Nickname })
            .ToListAsync();
        if (followers.Count == 0) return;

        var message = $"Нови данни за {modelName}: {authorNickname} добави отзив.";
        foreach (var f in followers)
        {
            _context.Notifications.Add(new Notification { UserId = f.UserId, Message = message, Url = url });
        }
        await _context.SaveChangesAsync();

        foreach (var f in followers)
        {
            await _hub.Clients.User(f.UserId).SendAsync("notification", new { message, url });

            if (!string.IsNullOrEmpty(f.Email))
            {
                var body = $"""
                    <p>Здравей, {WebUtility.HtmlEncode(f.Nickname)}!</p>
                    <p>Добавени са нови данни за <strong>{WebUtility.HtmlEncode(modelName)}</strong>, който следиш.</p>
                    <p><a href="{absoluteUrl}">Виж актуалната статистика</a></p>
                    <p style="color:#888">АвтоНадеждност.бг — можеш да спреш известията с бутона „Спри да следиш“ на страницата на модела.</p>
                    """;
                _emailQueue.Enqueue(new EmailMessage(f.Email, $"Нови данни за {modelName}", body));
            }
        }
    }

    public async Task NotifyUserAsync(string userId, string message, string? url)
    {
        _context.Notifications.Add(new Notification { UserId = userId, Message = message, Url = url });
        await _context.SaveChangesAsync();
        await _hub.Clients.User(userId).SendAsync("notification", new { message, url });
    }
}
