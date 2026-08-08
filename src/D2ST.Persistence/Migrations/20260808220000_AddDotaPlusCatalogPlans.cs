using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808220000_AddDotaPlusCatalogPlans")]
public partial class AddDotaPlusCatalogPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DotaPlusDays",
            table: "StoreCatalogItems",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DotaPlusDays",
            table: "StorePurchaseTransactions",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DotaPlusDays",
            table: "StorePurchaseTransactions");

        migrationBuilder.DropColumn(
            name: "DotaPlusDays",
            table: "StoreCatalogItems");
    }
}
