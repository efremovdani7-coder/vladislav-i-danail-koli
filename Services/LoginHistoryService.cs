using CarReliability.Data;
using CarReliability.Models;

namespace CarReliability.Services;

/// <summary>Записва в базата всяко влизане в системата (успешно или не).</summary>
public class LoginHistoryService
{
    private readonly ApplicationDbContext _context;

    public LoginHistoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecordAsync(HttpContext http, string login, ApplicationUser? user, string method, bool succeeded, string? failureReason = null)
    {
        var agent = http.Request.Headers.UserAgent.ToString();
        _context.LoginRecords.Add(new LoginRecord
        {
            UserId = user?.Id,
            Nickname = user?.Nickname,
            Login = Cut(login, 256),
            LoggedAt = DateTime.UtcNow,
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Cut(agent, 300),
            Method = method,
            Succeeded = succeeded,
            FailureReason = failureReason
        });
        await _context.SaveChangesAsync();
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
