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

    // Каталог
    public DbSet<CarBrand> CarBrands => Set<CarBrand>();
    public DbSet<CarModel> CarModels => Set<CarModel>();
    public DbSet<CarGeneration> CarGenerations => Set<CarGeneration>();
    public DbSet<CarEngine> CarEngines => Set<CarEngine>();

    // Потребителски данни
    public DbSet<CarEntry> CarEntries => Set<CarEntry>();
    public DbSet<ModelComment> ModelComments => Set<ModelComment>();
    public DbSet<ModelRating> ModelRatings => Set<ModelRating>();
    public DbSet<ModelFollow> ModelFollows => Set<ModelFollow>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<LoginRecord> LoginRecords => Set<LoginRecord>();
    public DbSet<CatalogImport> CatalogImports => Set<CatalogImport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e => e.HasIndex(u => u.Nickname).IsUnique());

        builder.Entity<CarBrand>(e => e.HasIndex(b => b.Name).IsUnique());

        builder.Entity<CarModel>(e =>
        {
            e.HasIndex(m => new { m.CarBrandId, m.Name });
            e.HasOne(m => m.CarBrand).WithMany(b => b.Models).HasForeignKey(m => m.CarBrandId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CarGeneration>(e =>
            e.HasIndex(g => new { g.CarModelId, g.Source }));
        builder.Entity<CarGeneration>(e =>
            e.HasOne(g => g.CarModel).WithMany(m => m.Generations).HasForeignKey(g => g.CarModelId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<CarEngine>(e =>
            e.HasOne(x => x.CarGeneration).WithMany(g => g.Engines).HasForeignKey(x => x.CarGenerationId).OnDelete(DeleteBehavior.Cascade));

        builder.Entity<CarEntry>(e =>
        {
            e.HasIndex(x => x.CarModelId);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.User).WithMany(u => u.CarEntries).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CarModel).WithMany().HasForeignKey(x => x.CarModelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CarGeneration).WithMany().HasForeignKey(x => x.CarGenerationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CarEngine).WithMany().HasForeignKey(x => x.CarEngineId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ModelComment>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CarModel).WithMany().HasForeignKey(x => x.CarModelId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelRating>(e =>
        {
            e.HasIndex(x => new { x.CarModelId, x.UserId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CarModel).WithMany().HasForeignKey(x => x.CarModelId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelFollow>(e =>
        {
            e.HasIndex(x => new { x.CarModelId, x.UserId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CarModel).WithMany().HasForeignKey(x => x.CarModelId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(x => new { x.SenderId, x.ReceiverId });
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Receiver).WithMany().HasForeignKey(x => x.ReceiverId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LoginRecord>(e =>
        {
            e.HasIndex(x => x.LoggedAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
