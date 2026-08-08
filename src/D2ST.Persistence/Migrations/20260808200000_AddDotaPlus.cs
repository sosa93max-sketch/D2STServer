using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808200000_AddDotaPlus")]
public partial class AddDotaPlus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DotaPlusAccounts",
            columns: table => new
            {
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                PlusFlags = table.Column<uint>(type: "INTEGER", nullable: false),
                SteamAgreementId = table.Column<ulong>(type: "INTEGER", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DotaPlusAccounts", x => x.AccountId));

        migrationBuilder.CreateTable(
            name: "DotaPlusTransactions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                ChangedByAccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                Days = table.Column<int>(type: "INTEGER", nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ExpiresAtAfter = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DotaPlusTransactions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_DotaPlusTransactions_AccountId_CreatedAt",
            table: "DotaPlusTransactions",
            columns: new[] { "AccountId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DotaPlusTransactions");
        migrationBuilder.DropTable(name: "DotaPlusAccounts");
    }
}
