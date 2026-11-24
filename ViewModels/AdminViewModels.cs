using CarReliability.Models;

namespace CarReliability.ViewModels;

public class AdminDashboardViewModel
{
    public List<CarEntry> Entries { get; set; } = new();
    public List<ApplicationUser> AllUsers { get; set; } = new();
    public int TotalEntries { get; set; }
    public int TotalUsers { get; set; }
    public int TotalComments { get; set; }
    public int LoginsToday { get; set; }
    public string? SearchTerm { get; set; }
    public string? FilterUser { get; set; }
}

public class AdminUserRow
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
    public int EntriesCount { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsBlocked { get; set; }
}

public class LoginHistoryViewModel
{
    public List<LoginRecord> Records { get; set; } = new();
    public string? Search { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
}

public class ConversationItem
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? LastText { get; set; }
    public DateTime? LastAt { get; set; }
    public int Unread { get; set; }
}

public class ChatViewModel
{
    public List<ConversationItem> Conversations { get; set; } = new();
    public List<ApplicationUser> OtherUsers { get; set; } = new();
    public ApplicationUser? Partner { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
    public string MyId { get; set; } = string.Empty;
}
