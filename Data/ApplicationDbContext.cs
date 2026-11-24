using CarReliability.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarReliability.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CarBrand> CarBrands { get; set; }
    public DbSet<CarEntry> CarEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.Nickname).IsUnique();
        });

        builder.Entity<CarBrand>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        builder.Entity<CarEntry>(entity =>
        {
            entity.HasIndex(e => new { e.CarBrandId, e.Model, e.Year });
            entity.HasIndex(e => e.ReliabilityRating);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.User)
                .WithMany(u => u.CarEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CarBrand)
                .WithMany(b => b.CarEntries)
                .HasForeignKey(e => e.CarBrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
