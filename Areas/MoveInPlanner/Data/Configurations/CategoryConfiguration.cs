using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoveInPlanner.Models.Entities;

namespace MoveInPlanner.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(
            new Category { Id = 1, Name = "Kitchen" },
            new Category { Id = 2, Name = "Cleaning" },
            new Category { Id = 3, Name = "Laundry" },
            new Category { Id = 4, Name = "Bathroom" },
            new Category { Id = 5, Name = "Bedroom" },
            new Category { Id = 6, Name = "Living Room" },
            new Category { Id = 7, Name = "Furniture" },
            new Category { Id = 8, Name = "Appliances" },
            new Category { Id = 9, Name = "Safety" },
            new Category { Id = 10, Name = "Tools" },
            new Category { Id = 11, Name = "Garden" },
            new Category { Id = 12, Name = "Moving Day" }
        );
    }
}
