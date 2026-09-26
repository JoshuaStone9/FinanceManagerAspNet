using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoveInPlanner.Data.Migrations;

public partial class AddProductChoiceImageUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "ProductChoices",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImageUrl",
            table: "ProductChoices");
    }
}
