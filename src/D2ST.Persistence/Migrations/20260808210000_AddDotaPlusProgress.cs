using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

[DbContext(typeof(D2stDbContext))]
[Migration("20260808210000_AddDotaPlusProgress")]
public partial class AddDotaPlusProgress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Shards",
            table: "DotaPlusAccounts",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "DotaPlusChallenges",
            columns: table => new
            {
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                SlotId = table.Column<uint>(type: "INTEGER", nullable: false),
                EventId = table.Column<uint>(type: "INTEGER", nullable: false),
                IntParam0 = table.Column<uint>(type: "INTEGER", nullable: false),
                IntParam1 = table.Column<uint>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Completed = table.Column<uint>(type: "INTEGER", nullable: false),
                SequenceId = table.Column<uint>(type: "INTEGER", nullable: false),
                ChallengeTier = table.Column<uint>(type: "INTEGER", nullable: false),
                Flags = table.Column<uint>(type: "INTEGER", nullable: false),
                Attempts = table.Column<uint>(type: "INTEGER", nullable: false),
                CompleteLimit = table.Column<uint>(type: "INTEGER", nullable: false),
                QuestRank = table.Column<uint>(type: "INTEGER", nullable: false),
                MaxQuestRank = table.Column<uint>(type: "INTEGER", nullable: false),
                InstanceId = table.Column<uint>(type: "INTEGER", nullable: false),
                HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                TemplateId = table.Column<uint>(type: "INTEGER", nullable: false),
                LastMatchReference = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DotaPlusChallenges", x => new { x.AccountId, x.SlotId }));

        migrationBuilder.CreateTable(
            name: "DotaPlusShardTransactions",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                ChangedByAccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                Amount = table.Column<long>(type: "INTEGER", nullable: false),
                BalanceAfter = table.Column<long>(type: "INTEGER", nullable: false),
                Reference = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DotaPlusShardTransactions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_DotaPlusShardTransactions_AccountId_CreatedAt",
            table: "DotaPlusShardTransactions",
            columns: new[] { "AccountId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_DotaPlusShardTransactions_Reference",
            table: "DotaPlusShardTransactions",
            column: "Reference",
            unique: true);

        migrationBuilder.CreateTable(
            name: "DotaPlusRelics",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                RelicRarity = table.Column<int>(type: "INTEGER", nullable: false),
                KillEaterType = table.Column<uint>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DotaPlusRelics", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_DotaPlusRelics_AccountId_HeroId_RelicRarity",
            table: "DotaPlusRelics",
            columns: new[] { "AccountId", "HeroId", "RelicRarity" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DotaPlusRelics");
        migrationBuilder.DropTable(name: "DotaPlusShardTransactions");
        migrationBuilder.DropTable(name: "DotaPlusChallenges");
        migrationBuilder.DropColumn(name: "Shards", table: "DotaPlusAccounts");
    }
}
