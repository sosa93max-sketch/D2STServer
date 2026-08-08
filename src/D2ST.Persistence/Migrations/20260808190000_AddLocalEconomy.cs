using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808190000_AddLocalEconomy")]
public partial class AddLocalEconomy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Wallets",
            columns: table => new
            {
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                BalanceCredits = table.Column<long>(type: "INTEGER", nullable: false),
                ReservedCredits = table.Column<long>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Wallets", x => x.AccountId));

        migrationBuilder.CreateTable(
            name: "WalletTransactions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                AmountCredits = table.Column<long>(type: "INTEGER", nullable: false),
                BalanceAfterCredits = table.Column<long>(type: "INTEGER", nullable: false),
                Reference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WalletTransactions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "StoreCatalogItems",
            columns: table => new
            {
                ProductId = table.Column<uint>(type: "INTEGER", nullable: false),
                DefIndex = table.Column<uint>(type: "INTEGER", nullable: false),
                ProductType = table.Column<int>(type: "INTEGER", nullable: false),
                PriceCredits = table.Column<long>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                BuildVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                Active = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_StoreCatalogItems", x => x.ProductId));

        migrationBuilder.CreateTable(
            name: "StoreCatalogComponents",
            columns: table => new
            {
                ProductId = table.Column<uint>(type: "INTEGER", nullable: false),
                ComponentProductId = table.Column<uint>(type: "INTEGER", nullable: false),
                Quantity = table.Column<uint>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_StoreCatalogComponents", x => new { x.ProductId, x.ComponentProductId }));

        migrationBuilder.CreateTable(
            name: "EconItems",
            columns: table => new
            {
                ItemId = table.Column<ulong>(type: "INTEGER", nullable: false),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                DefIndex = table.Column<uint>(type: "INTEGER", nullable: false),
                Quantity = table.Column<uint>(type: "INTEGER", nullable: false),
                Level = table.Column<uint>(type: "INTEGER", nullable: false),
                Quality = table.Column<uint>(type: "INTEGER", nullable: false),
                Flags = table.Column<uint>(type: "INTEGER", nullable: false),
                Origin = table.Column<uint>(type: "INTEGER", nullable: false),
                Inventory = table.Column<uint>(type: "INTEGER", nullable: false),
                Style = table.Column<uint>(type: "INTEGER", nullable: false),
                OriginalId = table.Column<ulong>(type: "INTEGER", nullable: false),
                EquippedStatesJson = table.Column<string>(type: "TEXT", nullable: false),
                AttributesJson = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EconItems", x => x.ItemId));

        migrationBuilder.CreateTable(
            name: "StorePurchaseTransactions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                TotalCredits = table.Column<long>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                LinesJson = table.Column<string>(type: "TEXT", nullable: false),
                GrantsJson = table.Column<string>(type: "TEXT", nullable: false),
                ItemIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_StorePurchaseTransactions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_EconItems_AccountId_DefIndex",
            table: "EconItems",
            columns: new[] { "AccountId", "DefIndex" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreCatalogItems_Active_ProductType",
            table: "StoreCatalogItems",
            columns: new[] { "Active", "ProductType" });

        migrationBuilder.CreateIndex(
            name: "IX_StorePurchaseTransactions_AccountId_Status",
            table: "StorePurchaseTransactions",
            columns: new[] { "AccountId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WalletTransactions_AccountId_CreatedAt",
            table: "WalletTransactions",
            columns: new[] { "AccountId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_WalletTransactions_Reference",
            table: "WalletTransactions",
            column: "Reference",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EconItems");
        migrationBuilder.DropTable(name: "StoreCatalogComponents");
        migrationBuilder.DropTable(name: "StoreCatalogItems");
        migrationBuilder.DropTable(name: "StorePurchaseTransactions");
        migrationBuilder.DropTable(name: "WalletTransactions");
        migrationBuilder.DropTable(name: "Wallets");
    }
}
