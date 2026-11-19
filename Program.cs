using CarReliability.Data;
using CarReliability.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // Create roles
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    if (!await roleManager.RoleExistsAsync("User"))
        await roleManager.CreateAsync(new IdentityRole("User"));

    // Create default admin
    if (await userManager.FindByEmailAsync("admin@cars.bg") == null)
    {
        var admin = new ApplicationUser
        {
            UserName = "admin@cars.bg",
            Email = "admin@cars.bg",
            Nickname = "Администратор",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "admin123");
        await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Seed sample car brands
    if (!context.CarBrands.Any())
    {
        var brands = new[]
        {
            new CarBrand { Name = "Toyota" },
            new CarBrand { Name = "BMW" },
            new CarBrand { Name = "Mercedes-Benz" },
            new CarBrand { Name = "Volkswagen" },
            new CarBrand { Name = "Audi" },
            new CarBrand { Name = "Honda" },
            new CarBrand { Name = "Ford" },
            new CarBrand { Name = "Opel" },
            new CarBrand { Name = "Peugeot" },
            new CarBrand { Name = "Renault" },
            new CarBrand { Name = "Hyundai" },
            new CarBrand { Name = "Kia" },
            new CarBrand { Name = "Mazda" },
            new CarBrand { Name = "Nissan" },
            new CarBrand { Name = "Skoda" },
            new CarBrand { Name = "Volvo" },
            new CarBrand { Name = "Fiat" },
            new CarBrand { Name = "Citroen" },
            new CarBrand { Name = "Dacia" },
            new CarBrand { Name = "Subaru" },
        };
        context.CarBrands.AddRange(brands);
        await context.SaveChangesAsync();
    }
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
