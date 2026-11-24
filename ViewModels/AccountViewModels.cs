using System.ComponentModel.DataAnnotations;

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
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Паролата трябва да е поне 4 символа.")]
    [DataType(DataType.Password)]
    [Display(Name = "Парола")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Потвърди паролата")]
    [Compare("Password", ErrorMessage = "Паролите не съвпадат.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress]
    [Display(Name = "Имейл")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    [DataType(DataType.Password)]
    [Display(Name = "Парола")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запомни ме")]
    public bool RememberMe { get; set; }
}
