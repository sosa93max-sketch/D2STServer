using System.Data;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Persistence;

/// <summary>
/// Applies the EF schema and bridges databases created by the old
/// EnsureCreated/SQL bootstrap into the migration history without deleting
/// their data.
/// </summary>
public static class D2stDatabaseMigrator
{
    public const string InitialMigrationId = "20260808144219_InitialSchema";

    public static bool NeedsLegacyBootstrap(D2stDbContext db) =>
        HasTable(db, "Accounts") && !HasTable(db, "__EFMigrationsHistory");

    public static void MarkLegacyBaseline(D2stDbContext db)
    {
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20260808144219_InitialSchema', '10.0.10');
            """);
    }

    private static bool HasTable(D2stDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM \"sqlite_master\" " +
                "WHERE \"type\" = 'table' AND \"name\" = $tableName;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }
}
