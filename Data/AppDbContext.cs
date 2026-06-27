using Microsoft.EntityFrameworkCore;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();
    public DbSet<LoanRecord> LoanRecords => Set<LoanRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        model.Entity<ItemTag>()
            .HasKey(it => new { it.ItemId, it.TagId });

        model.Entity<ItemTag>()
            .HasOne(it => it.Item)
            .WithMany(i => i.ItemTags)
            .HasForeignKey(it => it.ItemId);

        model.Entity<ItemTag>()
            .HasOne(it => it.Tag)
            .WithMany(t => t.ItemTags)
            .HasForeignKey(it => it.TagId);

        model.Entity<Item>()
            .HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<Item>()
            .HasOne(i => i.Location)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        model.Entity<MaintenanceLog>()
            .HasOne(m => m.Item)
            .WithMany(i => i.MaintenanceLogs)
            .HasForeignKey(m => m.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<LoanRecord>()
            .HasOne(l => l.Item)
            .WithMany(i => i.LoanRecords)
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<Item>().Property(i => i.PurchasePrice).HasPrecision(18, 2);
        model.Entity<Item>().Property(i => i.CurrentValue).HasPrecision(18, 2);
        model.Entity<MaintenanceLog>().Property(m => m.Cost).HasPrecision(18, 2);

        model.Entity<Item>().HasIndex(i => i.Name);
        model.Entity<Item>().HasIndex(i => i.Status);
        model.Entity<Item>().HasIndex(i => i.CategoryId);
        model.Entity<Item>().HasIndex(i => i.LocationId);
        model.Entity<Item>().HasIndex(i => i.SerialNumber);
        model.Entity<Item>().HasIndex(i => i.Barcode);
    }
}