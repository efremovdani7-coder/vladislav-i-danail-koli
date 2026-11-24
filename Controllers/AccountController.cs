using System.Security.Claims;
using CarReliability.Data;
using CarReliability.Models;
using CarReliability.Services;
using CarReliability.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly LoginHistoryService _loginHistory;
    private readonly ApplicationDbContext _context;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        LoginHistoryService loginHistory, ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _loginHistory = loginHistory;
        _context = context;
    }

    // ---------- Регистрация (US05) ----------

    public async Task<IActionResult> Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ExternalProviders = await _signInManager.GetExternalAuthenticationSchemesAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewBag.ExternalProviders = await _signInManager.GetExternalAuthenticationSchemesAsync();
        if (!ModelState.IsValid) return View(model);

        if (await NicknameTakenAsync(model.Nickname))
        {
            ModelState.AddModelError(nameof(model.Nickname), "Този никнейм вече е зает. Изберете друг.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            Nickname = model.Nickname,
            RegisteredAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _loginHistory.RecordAsync(HttpContext, user.Email!, user, "Регистрация", true);
            TempData["Success"] = $"Добре дошли, {user.Nickname}!";
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    // ---------- Вход ----------

    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.ExternalProviders = await _signInManager.GetExternalAuthenticationSchemesAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewBag.ExternalProviders = await _signInManager.GetExternalAuthenticationSchemesAsync();
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        var result = user == null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            await _loginHistory.RecordAsync(HttpContext, model.Email, user, "Парола", true);
            TempData["Success"] = $"Добре дошли отново, {user!.Nickname}!";
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        if (result.IsLockedOut)
        {
            await _loginHistory.RecordAsync(HttpContext, model.Email, user, "Парола", false, "Блокиран акаунт");
            ModelState.AddModelError(string.Empty, "Акаунтът ви е блокиран от администратор.");
            return View(model);
        }

        await _loginHistory.RecordAsync(HttpContext, model.Email, user, "Парола", false,
            user == null ? "Несъществуващ имейл" : "Грешна парола");
        ModelState.AddModelError(string.Empty, "Невалиден имейл или парола.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        TempData["Success"] = "Излязохте от профила си.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    // ---------- Вход с Google / Facebook (US12) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            TempData["Error"] = $"Грешка от външния доставчик: {remoteError}";
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["Error"] = "Неуспешен вход чрез външен профил.";
            return RedirectToAction(nameof(Login));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        var existing = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        if (result.Succeeded)
        {
            await _loginHistory.RecordAsync(HttpContext, email, existing, info.ProviderDisplayName ?? info.LoginProvider, true);
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }
        if (result.IsLockedOut)
        {
            await _loginHistory.RecordAsync(HttpContext, email, existing, info.ProviderDisplayName ?? info.LoginProvider, false, "Блокиран акаунт");
            TempData["Error"] = "Акаунтът ви е блокиран от администратор.";
            return RedirectToAction(nameof(Login));
        }

        // Има ли вече регистрация със същия имейл? → свързваме профила
        if (!string.IsNullOrEmpty(email))
        {
            var byEmail = await _userManager.FindByEmailAsync(email);
            if (byEmail != null)
            {
                if (await _userManager.IsLockedOutAsync(byEmail))
                {
                    TempData["Error"] = "Акаунтът ви е блокиран от администратор.";
                    return RedirectToAction(nameof(Login));
                }
                await _userManager.AddLoginAsync(byEmail, info);
                await _signInManager.SignInAsync(byEmail, isPersistent: false);
                await _loginHistory.RecordAsync(HttpContext, email, byEmail, info.ProviderDisplayName ?? info.LoginProvider, true);
                return LocalRedirect(SafeReturnUrl(returnUrl));
            }
        }

        // Нов потребител → задължителен избор на никнейм
        return View("ExternalNickname", new ExternalNicknameViewModel
        {
            Email = email,
            Provider = info.ProviderDisplayName ?? info.LoginProvider,
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalNickname(ExternalNicknameViewModel model)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["Error"] = "Сесията за вход изтече. Опитайте отново.";
            return RedirectToAction(nameof(Login));
        }

        model.Email = info.Principal.FindFirstValue(ClaimTypes.Email) ?? model.Email;
        model.Provider = info.ProviderDisplayName ?? info.LoginProvider;

        if (string.IsNullOrEmpty(model.Email))
            ModelState.AddModelError(string.Empty, "Външният профил не предостави имейл. Регистрирайте се с имейл и парола.");
        if (ModelState.IsValid && await NicknameTakenAsync(model.Nickname))
            ModelState.AddModelError(nameof(model.Nickname), "Този никнейм вече е зает. Изберете друг.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            Nickname = model.Nickname,
            RegisteredAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "User");
            await _userManager.AddLoginAsync(user, info);
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _loginHistory.RecordAsync(HttpContext, user.Email!, user, model.Provider, true);
            TempData["Success"] = $"Добре дошли, {user.Nickname}!";
            return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    // ---------- Профил ----------

    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        return View(new ProfileViewModel
        {
            User = user,
            Entries = await _context.CarEntries
                .Include(e => e.CarModel).ThenInclude(m => m!.CarBrand)
                .Include(e => e.CarEngine)
                .Where(e => e.UserId == user.Id)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(),
            Follows = await _context.ModelFollows
                .Include(f => f.CarModel).ThenInclude(m => m!.CarBrand)
                .Where(f => f.UserId == user.Id)
                .OrderBy(f => f.CarModel!.CarBrand!.Name).ThenBy(f => f.CarModel!.Name)
                .ToListAsync(),
            RecentLogins = await _context.LoginRecords
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.LoggedAt)
                .Take(10)
                .ToListAsync()
        });
    }

    private async Task<bool> NicknameTakenAsync(string nickname)
    {
        var lower = nickname.Trim().ToLower();
        var nicknames = await _context.Users.Select(u => u.Nickname).ToListAsync();
        // сравнението е без значение от малки/главни букви (вкл. кирилица)
        return nicknames.Any(n => n.ToLowerInvariant() == lower.ToLowerInvariant());
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home")!;
}
