using System.ComponentModel.DataAnnotations;
using CarReliability.Models;

namespace CarReliability.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Никнеймът е задължителен.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Никнеймът трябва да е между 3 и 20 символа.")]
    [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ0-9_]+$", ErrorMessage = "Никнеймът може да съдържа само букви, цифри и долна черта (_).")]
    [Display(Name = "Никнейм")]
    public string Nickname { get; set; } = string.Empty;

    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress(ErrorMessage = "Невалиден имейл адрес.")]
    [Display(Name = "Имейл")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Паролата трябва да е поне 6 символа.")]
    [DataType(DataType.Password)]
    [Display(Name = "Парола")]
    public string Password { get; set; } = string.Empty;

    // Паролата се въвежда два пъти, за да няма грешка при писане
    [Required(ErrorMessage = "Въведете паролата още веднъж.")]
    [DataType(DataType.Password)]
    [Display(Name = "Повторете паролата")]
    [Compare(nameof(Password), ErrorMessage = "Двете пароли не съвпадат.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress(ErrorMessage = "Невалиден имейл адрес.")]
    [Display(Name = "Имейл")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    [DataType(DataType.Password)]
    [Display(Name = "Парола")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запомни ме")]
    public bool RememberMe { get; set; }
}

/// <summary>US12: след вход с Google/Facebook новият потребител избира никнейм.</summary>
public class ExternalNicknameViewModel
{
    [Required(ErrorMessage = "Никнеймът е задължителен.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Никнеймът трябва да е между 3 и 20 символа.")]
    [RegularExpression(@"^[a-zA-Zа-яА-ЯёЁ0-9_]+$", ErrorMessage = "Никнеймът може да съдържа само букви, цифри и долна черта (_).")]
    [Display(Name = "Никнейм")]
    public string Nickname { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<CarEntry> Entries { get; set; } = new();
    public List<ModelFollow> Follows { get; set; } = new();
    public List<LoginRecord> RecentLogins { get; set; } = new();
}
