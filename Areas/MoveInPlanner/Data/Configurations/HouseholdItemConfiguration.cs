using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoveInPlanner.Models.Entities;

namespace MoveInPlanner.Data.Configurations;

public class HouseholdItemConfiguration : IEntityTypeConfiguration<HouseholdItem>
{
    public void Configure(EntityTypeBuilder<HouseholdItem> builder)
    {
        builder.HasOne(x => x.Category)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ProductChoices)
            .WithOne(x => x.HouseholdItem)
            .HasForeignKey(x => x.HouseholdItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SelectedProductChoice)
            .WithMany()
            .HasForeignKey(x => x.SelectedProductChoiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.Status, x.Priority });
    }
}
