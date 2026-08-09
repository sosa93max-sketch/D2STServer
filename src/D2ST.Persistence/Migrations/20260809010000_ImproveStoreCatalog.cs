using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260809010000_ImproveStoreCatalog")]
public partial class ImproveStoreCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "HeroesJson",
            table: "StoreCatalogItems",
            type: "TEXT",
            maxLength: 4096,
            nullable: false,
            defaultValue: "[]");

        // A pre-existing installation may contain duplicate item rows from
        // imports that used a different ProductId for the same DefIndex. Keep
        // the lowest ProductId and its ownership/purchase history untouched.
        migrationBuilder.Sql(
            """
            DELETE FROM "StoreCatalogComponents"
            WHERE "ProductId" IN (
                SELECT duplicate."ProductId"
                FROM "StoreCatalogItems" AS duplicate
                WHERE duplicate."ProductType" = 0
                  AND duplicate."DefIndex" > 0
                  AND EXISTS (
                      SELECT 1
                      FROM "StoreCatalogItems" AS keeper
                      WHERE keeper."ProductType" = duplicate."ProductType"
                        AND keeper."DefIndex" = duplicate."DefIndex"
                        AND keeper."ProductId" < duplicate."ProductId"));

            DELETE FROM "StoreCatalogItems"
            WHERE "ProductType" = 0
              AND "DefIndex" > 0
              AND EXISTS (
                  SELECT 1
                  FROM "StoreCatalogItems" AS keeper
                  WHERE keeper."ProductType" = "StoreCatalogItems"."ProductType"
                    AND keeper."DefIndex" = "StoreCatalogItems"."DefIndex"
                    AND keeper."ProductId" < "StoreCatalogItems"."ProductId");
            """);

        migrationBuilder.CreateIndex(
            name: "IX_StoreCatalogItems_ProductType_DefIndex",
            table: "StoreCatalogItems",
            columns: new[] { "ProductType", "DefIndex" },
            unique: true,
            filter: "ProductType = 0 AND DefIndex > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StoreCatalogItems_ProductType_DefIndex",
            table: "StoreCatalogItems");

        migrationBuilder.DropColumn(
            name: "HeroesJson",
            table: "StoreCatalogItems");
    }
}
