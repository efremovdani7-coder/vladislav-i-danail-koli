using CarReliability.Data;
using CarReliability.Hubs;
using CarReliability.Models;
using CarReliability.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// База данни: PostgreSQL сървър (напр. безплатна облачна база в Neon) или, ако няма въведена
// връзка, локален SQLite файл avtonadejdnost.db — за да може сайтът винаги да стартира.
var database = DatabaseSettings.Resolve(builder.Configuration);
builder.Services.AddSingleton(database);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (database.IsPostgres)
        options.UseNpgsql(database.ConnectionString);
    else
        options.UseSqlite(database.ConnectionString);
});

// Потребители и роли (ASP.NET Core Identity)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddErrorDescriber<BulgarianIdentityErrorDescriber>();

// Блокираните потребители се изхвърлят до 1 минута
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// US12: вход с Google / Facebook — включва се само ако има ключове в appsettings.json
var auth = builder.Services.AddAuthentication();
var googleId = builder.Configuration["Authentication:Google:ClientId"];
if (!string.IsNullOrWhiteSpace(googleId))
{
    auth.AddGoogle(o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    });
}
var facebookId = builder.Configuration["Authentication:Facebook:AppId"];
if (!string.IsNullOrWhiteSpace(facebookId))
{
    auth.AddFacebook(o =>
    {
        o.AppId = facebookId;
        o.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? "";
    });
}

// Услуги на приложението
builder.Services.AddScoped<ReliabilityService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LoginHistoryService>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailBackgroundService>();

// Публични бази данни за автомобили: FuelEconomy.gov (EPA) и NHTSA vPIC (VIN)
builder.Services.AddHttpClient("public-data", c =>
{
    c.Timeout = TimeSpan.FromMinutes(5);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("AvtoNadejdnost/1.0 (school project)");
});
builder.Services.AddSingleton<PublicCatalogImporter>();
builder.Services.AddScoped<VinDecoder>();
builder.Services.AddHostedService<PublicCatalogImportService>();
builder.Services.AddScoped<TestDataSeeder>();

builder.Services.AddSignalR();
builder.Services.AddControllersWithViews(options =>
{
    // Съобщения за грешни/липсващи стойности на български
    var m = options.ModelBindingMessageProvider;
    m.SetValueMustNotBeNullAccessor(_ => "Полето е задължително.");
    m.SetMissingBindRequiredValueAccessor(_ => "Полето е задължително.");
    m.SetMissingKeyOrValueAccessor(() => "Полето е задължително.");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "Липсват данни.");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"Стойността „{value}“ е невалидна.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Стойността „{value}“ е невалидна.");
    m.SetUnknownValueIsInvalidAccessor(_ => "Невалидна стойност.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Невалидна стойност.");
    m.SetValueIsInvalidAccessor(value => $"Стойността „{value}“ е невалидна.");
    m.SetValueMustBeANumberAccessor(_ => "Полето трябва да е число.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "Полето трябва да е число.");
});

var app = builder.Build();

app.Logger.LogInformation("База данни: {Database}", database.Description);
if (database.Warning != null) app.Logger.LogWarning("{Warning}", database.Warning);

// Създаване на базата, администратора и зареждане на каталога
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Environment, app.Configuration, app.Logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/hubs/chat");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
