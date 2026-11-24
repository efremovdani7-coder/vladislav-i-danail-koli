using System.Text.Json;
using System.Text.Json.Serialization;
using CarReliability.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Data;

/// <summary>Създава базата, ролите, администратора и зарежда каталога с автомобили.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IWebHostEnvironment env, IConfiguration config, ILogger logger)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = config["AdminAccount:Email"] ?? "admin@cars.bg";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                Nickname = config["AdminAccount:Nickname"] ?? "Администратор",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, config["AdminAccount:Password"] ?? "admin123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
            else
                logger.LogError("Неуспешно създаване на администратор: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await context.CarBrands.AnyAsync())
            await SeedCatalogAsync(context, env, logger);
    }

    private static async Task SeedCatalogAsync(ApplicationDbContext context, IWebHostEnvironment env, ILogger logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "Seed", "car-catalog.json");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "car-catalog.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("Не е намерен каталогът с автомобили: {Path}", path);
            return;
        }

        logger.LogInformation("Зареждане на каталога с автомобили (първо стартиране, отнема няколко секунди)...");
        await using var stream = File.OpenRead(path);
        var catalog = await JsonSerializer.DeserializeAsync<CatalogFile>(stream);
        if (catalog == null) return;

        var brands = new List<CarBrand>();
        foreach (var b in catalog.Brands)
        {
            var brand = new CarBrand { Name = b.Name };
            foreach (var m in b.Models)
            {
                var model = new CarModel { Name = m.Name };
                foreach (var g in m.Generations)
                {
                    var generation = new CarGeneration { Name = g.Name, YearFrom = g.From, YearTo = g.To };
                    foreach (var e in g.Engines)
                    {
                        var fuel = Enum.TryParse<FuelType>(e.Fuel, out var f) ? f : FuelType.Petrol;
                        foreach (var v in e.Variants)
                        {
                            generation.Engines.Add(new CarEngine
                            {
                                Name = Truncate(e.Name, 150),
                                FuelType = fuel,
                                DisplacementCc = e.Cc,
                                PowerHp = e.Hp,
                                TorqueNm = e.Nm,
                                Transmission = Truncate(v.ElementAtOrDefault(0) ?? "Не е посочена", 100),
                                DriveType = Truncate(v.ElementAtOrDefault(1) ?? "Не е посочено", 50)
                            });
                        }
                    }
                    model.Generations.Add(generation);
                }
                brand.Models.Add(model);
            }
            brands.Add(brand);
        }

        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.CarBrands.AddRange(brands);
        await context.SaveChangesAsync();
        context.ChangeTracker.AutoDetectChangesEnabled = true;
        context.ChangeTracker.Clear();

        logger.LogInformation("Каталогът е зареден: {Brands} марки.", brands.Count);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed class CatalogFile
    {
        [JsonPropertyName("brands")] public List<BrandJson> Brands { get; set; } = new();
    }

    private sealed class BrandJson
    {
        [JsonPropertyName("n")] public string Name { get; set; } = "";
        [JsonPropertyName("m")] public List<ModelJson> Models { get; set; } = new();
    }

    private sealed class ModelJson
    {
        [JsonPropertyName("n")] public string Name { get; set; } = "";
        [JsonPropertyName("g")] public List<GenerationJson> Generations { get; set; } = new();
    }

    private sealed class GenerationJson
    {
        [JsonPropertyName("n")] public string Name { get; set; } = "";
        [JsonPropertyName("from")] public int From { get; set; }
        [JsonPropertyName("to")] public int To { get; set; }
        [JsonPropertyName("e")] public List<EngineJson> Engines { get; set; } = new();
    }

    private sealed class EngineJson
    {
        [JsonPropertyName("n")] public string Name { get; set; } = "";
        [JsonPropertyName("f")] public string Fuel { get; set; } = "";
        [JsonPropertyName("cc")] public int? Cc { get; set; }
        [JsonPropertyName("hp")] public int? Hp { get; set; }
        [JsonPropertyName("nm")] public int? Nm { get; set; }
        [JsonPropertyName("v")] public List<List<string>> Variants { get; set; } = new();
    }
}
