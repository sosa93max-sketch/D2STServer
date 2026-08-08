using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808230000_ConvertLocalCreditsToDollars")]
public partial class ConvertLocalCreditsToDollars : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "BalanceCredits",
            table: "Wallets",
            newName: "BalanceDollars");

        migrationBuilder.RenameColumn(
            name: "ReservedCredits",
            table: "Wallets",
            newName: "ReservedDollars");

        migrationBuilder.RenameColumn(
            name: "AmountCredits",
            table: "WalletTransactions",
            newName: "AmountDollars");

        migrationBuilder.RenameColumn(
            name: "BalanceAfterCredits",
            table: "WalletTransactions",
            newName: "BalanceAfterDollars");

        migrationBuilder.RenameColumn(
            name: "PriceCredits",
            table: "StoreCatalogItems",
            newName: "PriceDollars");

        migrationBuilder.RenameColumn(
            name: "TotalCredits",
            table: "StorePurchaseTransactions",
            newName: "TotalDollars");

        // The previous unit was USD minor units: 100 credits represented $1.
        // Preserve the monetary value while changing the persisted unit to
        // whole local dollars. The local admin flow now creates dollar values
        // directly, so a value of 1 means $1.00 after this migration.
        migrationBuilder.Sql(
            """
            UPDATE "Wallets"
            SET "BalanceDollars" = "BalanceDollars" / 100,
                "ReservedDollars" = "ReservedDollars" / 100;
            UPDATE "WalletTransactions"
            SET "AmountDollars" = "AmountDollars" / 100,
                "BalanceAfterDollars" = "BalanceAfterDollars" / 100;
            UPDATE "StoreCatalogItems"
            SET "PriceDollars" = "PriceDollars" / 100;
            UPDATE "StorePurchaseTransactions"
            SET "TotalDollars" = "TotalDollars" / 100;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restore the previous minor-unit representation before restoring the
        // historical column names.
        migrationBuilder.Sql(
            """
            UPDATE "Wallets"
            SET "BalanceDollars" = "BalanceDollars" * 100,
                "ReservedDollars" = "ReservedDollars" * 100;
            UPDATE "WalletTransactions"
            SET "AmountDollars" = "AmountDollars" * 100,
                "BalanceAfterDollars" = "BalanceAfterDollars" * 100;
            UPDATE "StoreCatalogItems"
            SET "PriceDollars" = "PriceDollars" * 100;
            UPDATE "StorePurchaseTransactions"
            SET "TotalDollars" = "TotalDollars" * 100;
            """);

        migrationBuilder.RenameColumn(
            name: "BalanceDollars",
            table: "Wallets",
            newName: "BalanceCredits");

        migrationBuilder.RenameColumn(
            name: "ReservedDollars",
            table: "Wallets",
            newName: "ReservedCredits");

        migrationBuilder.RenameColumn(
            name: "AmountDollars",
            table: "WalletTransactions",
            newName: "AmountCredits");

        migrationBuilder.RenameColumn(
            name: "BalanceAfterDollars",
            table: "WalletTransactions",
            newName: "BalanceAfterCredits");

        migrationBuilder.RenameColumn(
            name: "PriceDollars",
            table: "StoreCatalogItems",
            newName: "PriceCredits");

        migrationBuilder.RenameColumn(
            name: "TotalDollars",
            table: "StorePurchaseTransactions",
            newName: "TotalCredits");
    }
}
