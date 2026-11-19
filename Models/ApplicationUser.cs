using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CarReliability.Models;

public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "Никнеймът е задължителен.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Никнеймът трябва да е между 2 и 50 символа.")]
    [Display(Name = "Никнейм")]
    public string Nickname { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public ICollection<CarEntry> CarEntries { get; set; } = new List<CarEntry>();
}
