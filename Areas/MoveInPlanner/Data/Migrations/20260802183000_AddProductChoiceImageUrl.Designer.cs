using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MoveInPlanner.Data;

#nullable disable

namespace MoveInPlanner.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802183000_AddProductChoiceImageUrl")]
partial class AddProductChoiceImageUrl
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.8")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("MoveInPlanner.Models.Entities.Category", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
            b.Property<string>("Description").HasMaxLength(300).HasColumnType("nvarchar(300)");
            b.Property<string>("Name").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.HasKey("Id");
            b.HasIndex("Name").IsUnique();
            b.ToTable("Categories");
            b.HasData(
                new { Id = 1, Name = "Kitchen" }, new { Id = 2, Name = "Cleaning" },
                new { Id = 3, Name = "Laundry" }, new { Id = 4, Name = "Bathroom" },
                new { Id = 5, Name = "Bedroom" }, new { Id = 6, Name = "Living Room" },
                new { Id = 7, Name = "Furniture" }, new { Id = 8, Name = "Appliances" },
                new { Id = 9, Name = "Safety" }, new { Id = 10, Name = "Tools" },
                new { Id = 11, Name = "Garden" }, new { Id = 12, Name = "Moving Day" });
        });

        modelBuilder.Entity("MoveInPlanner.Models.Entities.HouseholdItem", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
            b.Property<decimal?>("ActualPurchasePrice").HasColumnType("decimal(10,2)");
            b.Property<int>("CategoryId").HasColumnType("int");
            b.Property<int>("ChoiceType").HasColumnType("int");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<string>("DecisionReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<string>("FullComparisonResponse").HasColumnType("nvarchar(max)");
            b.Property<string>("GeneralNotes").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            b.Property<bool>("IsEssentialForMoveIn").HasColumnType("bit");
            b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("nvarchar(150)");
            b.Property<DateTime?>("NeededBy").HasColumnType("datetime2");
            b.Property<int>("Priority").HasColumnType("int");
            b.Property<DateTime?>("PurchasedOn").HasColumnType("datetime2");
            b.Property<int>("QuantityRequired").HasColumnType("int");
            b.Property<int?>("SelectedProductChoiceId").HasColumnType("int");
            b.Property<int>("Status").HasColumnType("int");
            b.Property<decimal?>("TargetBudget").HasColumnType("decimal(10,2)");
            b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime2");
            b.HasKey("Id");
            b.HasIndex("CategoryId");
            b.HasIndex("SelectedProductChoiceId");
            b.HasIndex("Status", "Priority");
            b.ToTable("HouseholdItems");
        });

        modelBuilder.Entity("MoveInPlanner.Models.Entities.ProductChoice", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
            b.Property<int>("HouseholdItemId").HasColumnType("int");
            b.Property<bool>("IsPreferred").HasColumnType("bit");
            b.Property<bool>("IsPurchased").HasColumnType("bit");
            b.Property<string>("ImageUrl").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            b.Property<string>("Notes").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            b.Property<decimal>("Price").HasColumnType("decimal(10,2)");
            b.Property<DateTime?>("PriceCheckedOn").HasColumnType("datetime2");
            b.Property<string>("ProductUrl").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            b.Property<int>("Quantity").HasColumnType("int");
            b.Property<string>("Retailer").HasMaxLength(100).HasColumnType("nvarchar(100)");
            b.Property<int>("Tier").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("HouseholdItemId");
            b.ToTable("ProductChoices");
        });

        modelBuilder.Entity("MoveInPlanner.Models.Entities.HouseholdItem", b =>
        {
            b.HasOne("MoveInPlanner.Models.Entities.Category", "Category")
                .WithMany("Items").HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne("MoveInPlanner.Models.Entities.ProductChoice", "SelectedProductChoice")
                .WithMany().HasForeignKey("SelectedProductChoiceId").OnDelete(DeleteBehavior.NoAction);
            b.Navigation("Category");
            b.Navigation("SelectedProductChoice");
        });

        modelBuilder.Entity("MoveInPlanner.Models.Entities.ProductChoice", b =>
        {
            b.HasOne("MoveInPlanner.Models.Entities.HouseholdItem", "HouseholdItem")
                .WithMany("ProductChoices").HasForeignKey("HouseholdItemId").OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation("HouseholdItem");
        });

        modelBuilder.Entity("MoveInPlanner.Models.Entities.Category", b => b.Navigation("Items"));
        modelBuilder.Entity("MoveInPlanner.Models.Entities.HouseholdItem", b => b.Navigation("ProductChoices"));
#pragma warning restore 612, 618
    }
}
