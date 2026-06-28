using Microsoft.EntityFrameworkCore;
using FinanceManagerAspNet.Models;

namespace FinanceManagerAspNet.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ItemType> ItemTypes => Set<ItemType>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ItemTag> ItemTags => Set<ItemTag>();
    public DbSet<ItemPhoto> ItemPhotos => Set<ItemPhoto>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();
    public DbSet<LoanRecord> LoanRecords => Set<LoanRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        model.Entity<Tag>()
            .ToTable("Tags");

        model.Entity<Tag>()
            .HasKey(t => t.Id);

        model.Entity<ItemTag>()
            .ToTable("ItemTags");

        model.Entity<ItemTag>()
            .HasKey(it => new { it.ItemId, it.TagId });

        model.Entity<ItemTag>()
            .Property(it => it.TagId)
            .HasColumnName("TagId");

        model.Entity<ItemTag>()
            .HasOne(it => it.Tag)
            .WithMany(t => t.ItemTags)
            .HasForeignKey(it => it.TagId)
            .HasPrincipalKey(t => t.Id);

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

        model.Entity<Item>()
            .HasOne(i => i.ItemType)
            .WithMany(t => t.Items)
            .HasForeignKey(i => i.ItemTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<Item>()
            .HasOne(i => i.Platform)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PlatformId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<Platform>()
            .HasOne(p => p.ItemType)
            .WithMany(t => t.Platforms)
            .HasForeignKey(p => p.ItemTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        model.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        model.Entity<ItemPhoto>()
            .HasOne(p => p.Item)
            .WithMany(i => i.Photos)
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

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
        model.Entity<Item>().HasIndex(i => i.ItemTypeId);
        model.Entity<Item>().HasIndex(i => i.PlatformId);
        model.Entity<Item>().HasIndex(i => i.LegacyInventoryId);
        model.Entity<Item>().HasIndex(i => i.LegacyPlatformId);
        model.Entity<Item>().HasIndex(i => i.LegacyTypeId);
        model.Entity<Item>().HasIndex(i => i.Manufacturer);
        model.Entity<Item>().HasIndex(i => i.Owner);
        model.Entity<Item>().HasIndex(i => i.ReleaseYear);
        model.Entity<Item>().HasIndex(i => i.SerialNumber);
        model.Entity<Item>().HasIndex(i => i.Barcode);

        model.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        model.Entity<ItemType>().HasIndex(t => t.Name).IsUnique();
        model.Entity<ItemType>().HasIndex(t => t.LegacyTypeId);
        model.Entity<Platform>().HasIndex(p => new { p.Name, p.ItemTypeId }).IsUnique();
        model.Entity<Platform>().HasIndex(p => p.LegacyPlatformId);
        model.Entity<Location>().HasIndex(l => l.LegacyLocationId);
    }
}