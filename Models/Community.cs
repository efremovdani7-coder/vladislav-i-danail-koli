using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

/// <summary>US10: коментар към модел.</summary>
public class ModelComment
{
    public int Id { get; set; }

    public int CarModelId { get; set; }
    public CarModel? CarModel { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required(ErrorMessage = "Коментарът не може да е празен.")]
    [StringLength(1000, ErrorMessage = "Коментарът може да е до 1000 символа.")]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>US10: оценка 1–5 звезди — по една на потребител за модел.</summary>
public class ModelRating
{
    public int Id { get; set; }

    public int CarModelId { get; set; }
    public CarModel? CarModel { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Range(1, 5)]
    public int Stars { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>US11: „Следи модел“.</summary>
public class ModelFollow
{
    public int Id { get; set; }

    public int CarModelId { get; set; }
    public CarModel? CarModel { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>US11: известие в профила.</summary>
public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Url { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>US13: лично съобщение в чата.</summary>
public class ChatMessage
{
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser? Sender { get; set; }

    [Required]
    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser? Receiver { get; set; }

    [Required, StringLength(1000)]
    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

/// <summary>Запис за всяко влизане в системата (успешно или неуспешно).</summary>
public class LoginRecord
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(256)]
    [Display(Name = "Имейл / потребител")]
    public string Login { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Никнейм")]
    public string? Nickname { get; set; }

    [Display(Name = "Дата и час")]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [StringLength(64)]
    [Display(Name = "IP адрес")]
    public string? IpAddress { get; set; }

    [StringLength(300)]
    [Display(Name = "Браузър / устройство")]
    public string? UserAgent { get; set; }

    [StringLength(30)]
    [Display(Name = "Начин на вход")]
    public string Method { get; set; } = "Парола";

    [Display(Name = "Успешен")]
    public bool Succeeded { get; set; }

    [StringLength(100)]
    [Display(Name = "Причина")]
    public string? FailureReason { get; set; }
}
