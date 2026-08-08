using D2ST.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(D2stDbContext))]
[Migration("20260808170000_AddShowcases")]
public partial class AddShowcases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Showcases",
            columns: table => new
            {
                AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                ShowcaseType = table.Column<uint>(type: "INTEGER", nullable: false),
                FormatVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                Payload = table.Column<byte[]>(type: "BLOB", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Showcases", x => new { x.AccountId, x.ShowcaseType });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Showcases");
    }
}
