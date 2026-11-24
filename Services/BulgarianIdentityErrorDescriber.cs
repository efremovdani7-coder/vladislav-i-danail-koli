using Microsoft.AspNetCore.Identity;

namespace CarReliability.Services;

/// <summary>Съобщения за грешки при регистрация/вход на български.</summary>
public class BulgarianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = $"Имейлът {email} вече е регистриран." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Потребителят {userName} вече съществува." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Невалиден имейл адрес." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Невалидно потребителско име." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Паролата трябва да е поне {length} символа." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Грешна парола." };

    public override IdentityError LoginAlreadyAssociated() =>
        new() { Code = nameof(LoginAlreadyAssociated), Description = "Този външен профил вече е свързан с друг потребител." };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Възникна грешка. Опитайте отново." };
}
