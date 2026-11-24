using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

public class ApplicationUser : IdentityUser
{
    // US05: уникален никнейм, 3–20 символа (букви, цифри и _)
    [Required(ErrorMessage = "Никнеймът е задължителен.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Никнеймът трябва да е между 3 и 20 символа.")]
    [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ0-9_]+$", ErrorMessage = "Никнеймът може да съдържа само букви, цифри и долна черта (_).")]
    [Display(Name = "Никнейм")]
    public string Nickname { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public ICollection<CarEntry> CarEntries { get; set; } = new List<CarEntry>();
}
