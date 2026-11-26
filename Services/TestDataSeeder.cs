using CarReliability.Data;
using CarReliability.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Services;

/// <summary>
/// Тестови данни, за да може сайтът да се изпробва веднага: тестови потребители (никнейм „Тест_...“),
/// отзиви за популярни в България модели, коментари, звезди, следени модели, съобщения и влизания.
/// Всички тестови отзиви са отбелязани с „[Тестови данни]“, а потребителите — с IsTestAccount.
/// Администраторът може да ги изтрие с един бутон (Админ → Каталог и тестови данни).
/// </summary>
public class TestDataSeeder
{
    public const string Marker = "[Тестови данни]";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public TestDataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
    }

    private static readonly (string Nickname, string Email)[] Users =
    {
        ("Тест_Иван", "ivan@test.bg"), ("Тест_Мария", "maria@test.bg"), ("Тест_Георги", "georgi@test.bg"),
        ("Тест_Елена", "elena@test.bg"), ("Тест_Петър", "petar@test.bg"), ("Тест_Десислава", "desislava@test.bg"),
        ("Тест_Николай", "nikolay@test.bg"), ("Тест_Ани", "ani@test.bg")
    };

    // (марка, модел, склонност към надеждност: + по-надеждна, − по-проблемна) — само за правдоподобни тестови стойности
    private static readonly (string Brand, string Model, double Bias)[] Cars =
    {
        ("Toyota", "Corolla", 1.6), ("Toyota", "RAV4", 1.5), ("Toyota", "Yaris", 1.2), ("Honda", "Civic", 1.2),
        ("Mazda", "3", 1.0), ("Mazda", "CX-5", 1.0), ("Lexus", "RX", 1.4), ("Hyundai", "i30", 0.6), ("Kia", "Ceed", 0.6),
        ("Skoda", "Octavia", 0.5), ("Volvo", "XC60", 0.3), ("Volkswagen", "Golf", 0.3), ("Seat", "Leon", 0.1),
        ("Dacia", "Duster", 0.2), ("Dacia", "Sandero", 0.0), ("Volkswagen", "Passat", -0.1), ("Audi", "A4", -0.2),
        ("Mercedes-Benz", "C-Class", -0.3), ("Nissan", "Qashqai", -0.3), ("Ford", "Focus", -0.4),
        ("BMW", "3 Series", -0.5), ("Opel", "Astra", -0.5), ("Fiat", "500", -0.6), ("Peugeot", "308", -0.7),
        ("Renault", "Clio", -0.8), ("Citroen", "C4", -0.9), ("Renault", "Megane", -1.0), ("Alfa Romeo", "Giulietta", -1.2),
        ("Land Rover", "Range Rover", -1.6)
    };

    private static readonly string[] Issues =
    {
        "Смяна на съединител и маховик.", "Теч на масло от уплътненията на двигателя.", "Сменени тампони и носачи.",
        "Проблем с дебитомера, сменен.", "Сменени накладки и дискове по-рано от очакваното.", "Повреда в климатичния компресор.",
        "Грешки по таблото заради датчици.", "Корозия по праговете.", "Сменена водна помпа и ремък.", "Шум от лагер на колело.",
        "Проблем с инжекторите.", "Сменен акумулатор и стартер.", "Сменена дроселова клапа.", "Ремонт на скоростната кутия."
    };
    private static readonly string[] Pros =
    {
        "Икономичен разход.", "Удобно окачване.", "Евтини и лесно достъпни части.", "Просторен салон и багажник.",
        "Много добра шумоизолация.", "Пъргав двигател.", "Надежден в зимни условия.", "Добра видимост и лесно паркиране."
    };
    private static readonly string[] Cons =
    {
        "Скъпо обслужване в официален сервиз.", "Твърдо окачване на лоши пътища.", "Малко място отзад.",
        "Слаба мултимедия.", "Висок разход в града.", "Евтини материали в салона.", "Шумен на магистрала."
    };
    private static readonly string[] Comments =
    {
        "Карам такава вече няколко години — без сериозни проблеми.", "Внимавайте за ходовата част при покупка на втора ръка.",
        "Добър избор за семейството, но обслужването не е евтино.", "Някой има ли опит с автоматичната кутия на този модел?",
        "Части се намират лесно и не са скъпи.", "При мен разходът е около 7 литра в смесен режим."
    };

    public async Task<bool> HasTestDataAsync() => await _db.Users.AnyAsync(u => u.IsTestAccount);

    public async Task<int> SeedAsync()
    {
        if (await HasTestDataAsync()) return 0;
        var rnd = new Random(2026);
        var now = DateTime.UtcNow;
        var password = _config["TestData:Password"] ?? "test123";

        // 1. Тестови потребители
        var users = new List<ApplicationUser>();
        foreach (var (nickname, email) in Users)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email, Email = email, EmailConfirmed = true, Nickname = nickname, IsTestAccount = true,
                    RegisteredAt = now.AddDays(-rnd.Next(60, 400))
                };
                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded) continue;
                await _userManager.AddToRoleAsync(user, "User");
            }
            users.Add(user);
        }
        if (users.Count == 0) return 0;

        // 2. Отзиви
        var entries = new List<CarEntry>();
        var usedModels = new List<(CarModel Model, double Bias)>();
        foreach (var (brandName, modelName, bias) in Cars)
        {
            var model = await _db.CarModels.Include(m => m.CarBrand)
                .FirstOrDefaultAsync(m => m.CarBrand!.Name == brandName && m.Name == modelName);
            if (model == null) continue;

            var gens = await _db.CarGenerations.Include(g => g.Engines)
                .Where(g => g.CarModelId == model.Id && g.Source == CatalogSources.AutoEvolution && g.YearTo >= 2004 && g.YearFrom <= now.Year)
                .ToListAsync();
            gens = gens.Where(g => g.Engines.Any() && g.BodyType != BodyType.Convertible && g.BodyType != BodyType.Pickup).ToList();
            if (gens.Count == 0) continue;
            usedModels.Add((model, bias));

            var count = rnd.Next(3, 7);
            for (var i = 0; i < count; i++)
            {
                var gen = gens[rnd.Next(gens.Count)];
                var engineList = gen.Engines.ToList();
                var engine = engineList[rnd.Next(engineList.Count)];
                var yearTo = Math.Min(gen.YearTo, now.Year - 1);
                var year = yearTo < gen.YearFrom ? gen.YearFrom : rnd.Next(gen.YearFrom, yearTo + 1);
                var age = Math.Max(1, now.Year - year);

                var rating = (int)Math.Clamp(Math.Round(7.2 + bias - age * 0.09 + (rnd.NextDouble() - 0.5) * 2.4), 1, 10);
                var defectCount = Math.Clamp((int)Math.Round((10 - rating) / 2.5 + (rnd.NextDouble() - 0.5)), 0, 5);
                var defects = DefectCategory.None;
                var issueTexts = new List<string>();
                for (var d = 0; d < defectCount; d++)
                {
                    defects |= EnumText.Defects[rnd.Next(EnumText.Defects.Length - 1)].Value;
                    issueTexts.Add(Issues[rnd.Next(Issues.Length)]);
                }

                var cost = Math.Round((250 + age * 45 + defectCount * 180 + (1.5 - bias) * 120) * (0.7 + rnd.NextDouble() * 0.6) / 10) * 10;
                entries.Add(new CarEntry
                {
                    CarModelId = model.Id,
                    CarGenerationId = gen.Id,
                    CarEngineId = engine.Id,
                    Year = year,
                    Mileage = (int)(age * rnd.Next(11000, 23000) / 1000) * 1000,
                    MaintenanceCost = (decimal)Math.Max(150, cost),
                    ReliabilityRating = rating,
                    Defects = defects,
                    CommonIssues = issueTexts.Count > 0 ? string.Join(" ", issueTexts.Distinct()) : null,
                    Pros = Pros[rnd.Next(Pros.Length)] + " " + Pros[rnd.Next(Pros.Length)],
                    Cons = Cons[rnd.Next(Cons.Length)],
                    Comment = $"{Marker} Примерен отзив, създаден автоматично за изпробване на сайта.",
                    UserId = users[rnd.Next(users.Count)].Id,
                    CreatedAt = now.AddDays(-rnd.Next(1, 180)).AddMinutes(-rnd.Next(0, 1440))
                });
            }
        }
        _db.CarEntries.AddRange(entries);
        await _db.SaveChangesAsync();

        // 3. Звезди, коментари и следени модели
        foreach (var (model, bias) in usedModels)
        {
            foreach (var user in users.OrderBy(_ => rnd.Next()).Take(rnd.Next(2, 6)))
            {
                var stars = (int)Math.Clamp(Math.Round(3.4 + bias * 0.8 + (rnd.NextDouble() - 0.5) * 1.6), 1, 5);
                _db.ModelRatings.Add(new ModelRating { CarModelId = model.Id, UserId = user.Id, Stars = stars, CreatedAt = now.AddDays(-rnd.Next(1, 120)) });
            }
            if (rnd.NextDouble() < 0.6)
            {
                _db.ModelComments.Add(new ModelComment
                {
                    CarModelId = model.Id,
                    UserId = users[rnd.Next(users.Count)].Id,
                    Text = $"{Marker} {Comments[rnd.Next(Comments.Length)]}",
                    CreatedAt = now.AddDays(-rnd.Next(1, 90))
                });
            }
            if (rnd.NextDouble() < 0.3)
                _db.ModelFollows.Add(new ModelFollow { CarModelId = model.Id, UserId = users[rnd.Next(users.Count)].Id });
        }

        // 4. Няколко съобщения в чата
        if (users.Count >= 2)
        {
            var a = users[0]; var b = users[1];
            var texts = new[] { (a, b, "Здравей! Видях отзива ти — колко ти излиза поддръжката на година?"),
                                (b, a, "Здрасти! Около 600 €, без неочаквани ремонти."),
                                (a, b, "Благодаря, мисля си да си взема същия модел.") };
            var t = now.AddDays(-2);
            foreach (var (from, to, text) in texts)
            {
                t = t.AddMinutes(rnd.Next(3, 40));
                _db.ChatMessages.Add(new ChatMessage { SenderId = from.Id, ReceiverId = to.Id, Text = $"{text} {Marker}", SentAt = t, IsRead = true });
            }
        }

        // 5. История на влизанията
        foreach (var user in users)
        {
            for (var i = 0; i < rnd.Next(2, 6); i++)
            {
                var ok = rnd.NextDouble() > 0.15;
                _db.LoginRecords.Add(new LoginRecord
                {
                    UserId = user.Id, Nickname = user.Nickname, Login = user.Email!, Method = "Парола (тест)",
                    LoggedAt = now.AddDays(-rnd.Next(0, 60)).AddMinutes(-rnd.Next(0, 1440)),
                    IpAddress = $"192.168.1.{rnd.Next(2, 250)}", UserAgent = "Тестови данни",
                    Succeeded = ok, FailureReason = ok ? null : "Грешна парола"
                });
            }
        }

        await _db.SaveChangesAsync();
        return entries.Count;
    }

    /// <summary>Изтрива тестовите потребители и всичко, което са създали (отзиви, коментари, звезди, съобщения).</summary>
    public async Task<int> RemoveAsync()
    {
        var testUsers = await _db.Users.Where(u => u.IsTestAccount).ToListAsync();
        foreach (var user in testUsers)
            await _userManager.DeleteAsync(user);
        return testUsers.Count;
    }
}
