using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Models.Entities;

namespace MoveInPlanner.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<HouseholdItem> HouseholdItems => Set<HouseholdItem>();
    public DbSet<ProductChoice> ProductChoices => Set<ProductChoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
