namespace CarReliability.Data;

/// <summary>
/// Избира базата данни според appsettings.json → "Database:Provider":
///  • "PostgreSql" — сървър PostgreSQL (локален или облачен, напр. Neon). Връзката е в "ConnectionStrings:PostgreSql"
///    (за да не се качва паролата в GitHub, задайте я с: dotnet user-secrets set "ConnectionStrings:PostgreSql" "...").
///  • "Sqlite" — локален файл avtonadejdnost.db.
/// Ако е избран PostgreSQL, но няма въведена връзка, сайтът временно използва SQLite и пише предупреждение.
/// </summary>
public class DatabaseSettings
{
    public bool IsPostgres { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Warning { get; init; }

    public static DatabaseSettings Resolve(IConfiguration config)
    {
        var provider = config["Database:Provider"] ?? "PostgreSql";
        var sqlite = config.GetConnectionString("Sqlite") ?? "Data Source=avtonadejdnost.db";
        var postgres = config.GetConnectionString("PostgreSql");

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(postgres))
            {
                var cs = NormalizePostgres(postgres);
                return new DatabaseSettings
                {
                    IsPostgres = true,
                    ConnectionString = cs,
                    Description = "PostgreSQL сървър (" + HostOf(cs) + ")"
                };
            }

            return new DatabaseSettings
            {
                IsPostgres = false,
                ConnectionString = sqlite,
                Description = "SQLite файл (" + sqlite + ")",
                Warning = "Не е въведена връзка към PostgreSQL (ConnectionStrings:PostgreSql). " +
                          "Сайтът временно използва локалния SQLite файл. Вижте README.md → „Облачна база данни“."
            };
        }

        return new DatabaseSettings
        {
            IsPostgres = false,
            ConnectionString = sqlite,
            Description = "SQLite файл (" + sqlite + ")"
        };
    }

    /// <summary>Neon и други облачни услуги дават адрес във вида postgresql://user:pass@host/db?sslmode=require —
    /// превръщаме го във формата, която разбира Npgsql.</summary>
    public static string NormalizePostgres(string value)
    {
        value = value.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var parts = new List<string>
        {
            "Host=" + uri.Host,
            "Port=" + (uri.Port > 0 ? uri.Port : 5432),
            "Database=" + Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            "Username=" + Uri.UnescapeDataString(userInfo[0])
        };
        if (userInfo.Length > 1) parts.Add("Password=" + Uri.UnescapeDataString(userInfo[1]));

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sslMode = query.Select(q => q.Split('=', 2))
            .FirstOrDefault(q => q[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))?.ElementAtOrDefault(1);
        // "verify-full" → "VerifyFull", "require" → "Require"
        var mode = string.IsNullOrEmpty(sslMode) ? "Require"
            : string.Concat(sslMode.Split('-').Where(s => s.Length > 0).Select(s => char.ToUpper(s[0]) + s[1..].ToLower()));
        parts.Add("SSL Mode=" + mode);
        return string.Join(";", parts);
    }

    private static string HostOf(string connectionString) =>
        connectionString.Split(';')
            .Select(p => p.Split('=', 2))
            .FirstOrDefault(p => p[0].Trim().Equals("Host", StringComparison.OrdinalIgnoreCase))?.ElementAtOrDefault(1) ?? "?";
}
