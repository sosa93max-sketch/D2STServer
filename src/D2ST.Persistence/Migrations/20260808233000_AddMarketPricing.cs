using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808233000_AddMarketPricing")]
public partial class AddMarketPricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MarketHashName",
            table: "StoreCatalogItems",
            type: "TEXT",
            maxLength: 300,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<long>(
            name: "MarketLowestPriceCents",
            table: "StoreCatalogItems",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "MarketMedianPriceCents",
            table: "StoreCatalogItems",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MarketPriceSource",
            table: "StoreCatalogItems",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "MarketPriceStatus",
            table: "StoreCatalogItems",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "not_checked");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "MarketPriceUpdatedAt",
            table: "StoreCatalogItems",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "MarketVolume",
            table: "StoreCatalogItems",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MarketHashName",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketLowestPriceCents",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketMedianPriceCents",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketPriceSource",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketPriceStatus",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketPriceUpdatedAt",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "MarketVolume",
            table: "StoreCatalogItems");
    }
}
