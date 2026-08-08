using D2ST.Api;
using D2ST.Api.Economy;
using D2ST.Api.Endpoints;
using D2ST.Api.Logging;
using D2ST.Api.Matches;
using D2ST.Api.Profiles;
using D2ST.Api.Ranks;
using D2ST.Core.Profiles;
using D2ST.GameCoordinator;
using D2ST.GameCoordinator.DotaPlus;
using D2ST.GameCoordinator.Econ;
using D2ST.GameCoordinator.Matches;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Players;
using D2ST.GameCoordinator.Ranks;
using D2ST.Persistence;
using D2ST.Steam;
using D2ST.Api.DotaPlus;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("D2st") ?? "Data Source=Data/d2st.db";
EnsureSqliteDirectory(connectionString);

builder.Logging.AddFileLogger(builder.Configuration);

builder.Services.AddD2stPersistence(connectionString);
builder.Services.AddSteamServices(builder.Configuration);
builder.Services.AddHttpClient("SteamMarket", client =>
{
    client.BaseAddress = new Uri("https://steamcommunity.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("D2STServer/market-price-sync");
});
builder.Services.AddSingleton<IGcPlayerDirectory, SessionGcPlayerDirectory>();
builder.Services.AddSingleton<IGcMessageQueue, EventStreamGcMessageQueue>();
builder.Services.AddSingleton<IRankStore, RankStore>();
builder.Services.AddSingleton<IMatchStore, MatchStore>();
builder.Services.AddSingleton<IProfileStore, ProfileCardStore>();
builder.Services.AddSingleton<IShowcaseStore, ShowcaseStore>();
builder.Services.AddSingleton<IEconomyStore, EconomyStore>();
builder.Services.AddSingleton<SteamMarketPriceSync>();
builder.Services.AddSingleton<IDotaPlusStore, DotaPlusStore>();
builder.Services.AddSingleton<DotaCatalogImporter>();
builder.Services.AddGameCoordinator(builder.Configuration, builder.Environment.ContentRootPath);

// The shim serializes/deserializes with PascalCase member names, so keep the
// property names verbatim instead of camel-casing them.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// New databases and already-migrated installations use EF Core migrations.
// Databases created by the pre-migrations bootstrap take the one-time legacy
// path below, keep their rows and are then stamped at the initial migration.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();

    if (!D2stDatabaseMigrator.NeedsLegacyBootstrap(db))
    {
        db.Database.Migrate();
    }
    else
    {

    // Avatar was present in the model from the first release, but the legacy
    // bootstrap did not evolve an already existing SQLite database. Keep
    // installations created by an older build readable before UserDirectory
    // queries Accounts.Avatar.
    if (!HasColumn(db, "Accounts", "Avatar"))
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"Accounts\" ADD COLUMN \"Avatar\" BLOB NULL;");
    }

    // This block is retained only for a database created before migrations
    // existed. It creates missing tables without replacing existing rows.
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS "PlayerRanks" (
            "AccountId" INTEGER NOT NULL CONSTRAINT "PK_PlayerRanks" PRIMARY KEY,
            "Mmr" INTEGER NOT NULL,
            "Wins" INTEGER NOT NULL,
            "Losses" INTEGER NOT NULL,
            "Games" INTEGER NOT NULL,
            "IsCalibrated" INTEGER NOT NULL DEFAULT 0,
            "UpdatedAt" TEXT NOT NULL
        );
        """);

    // Existing databases were created before calibration was persisted. Check
    // the schema before adding the column so a newly created database does not
    // log a misleading duplicate-column error on every startup.
    if (!HasPlayerRankCalibrationColumn(db))
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"PlayerRanks\" ADD COLUMN \"IsCalibrated\" INTEGER NOT NULL DEFAULT 0;");
    }

    db.Database.ExecuteSqlRaw(
        "UPDATE \"PlayerRanks\" SET \"IsCalibrated\" = 1 WHERE \"Mmr\" > 0 AND \"IsCalibrated\" = 0;");

    // Local match history is the source for the profile projection. This is the
    // one-time compatibility path for the pre-migrations database; new and
    // already-migrated installations use the EF migration above.
    db.Database.ExecuteSqlRaw(
        """
        CREATE TABLE IF NOT EXISTS "Matches" (
            "MatchId" INTEGER NOT NULL CONSTRAINT "PK_Matches" PRIMARY KEY,
            "LobbyId" INTEGER NOT NULL,
            "GameMode" INTEGER NOT NULL,
            "DurationSeconds" INTEGER NOT NULL,
            "EndedAt" TEXT NOT NULL,
            "GoodGuysWin" INTEGER NOT NULL,
            "WinningTeam" INTEGER NOT NULL,
            "FirstBloodTime" INTEGER NOT NULL,
            "RadiantScore" INTEGER NOT NULL,
            "DireScore" INTEGER NOT NULL,
            "TowerStatusJson" TEXT NOT NULL,
            "BarracksStatusJson" TEXT NOT NULL,
            "TeamScoresJson" TEXT NOT NULL,
            "Cluster" INTEGER NOT NULL,
            "ServerAddress" TEXT NOT NULL,
            "EventScore" INTEGER NOT NULL,
            "AutomaticSurrender" INTEGER NOT NULL,
            "ServerVersion" INTEGER NOT NULL,
            "PreGameDuration" INTEGER NOT NULL,
            "AverageNetworthDelta" INTEGER NOT NULL,
            "MatchFlags" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_Matches_LobbyId" ON "Matches" ("LobbyId");
        CREATE INDEX IF NOT EXISTS "IX_Matches_EndedAt" ON "Matches" ("EndedAt");

        CREATE TABLE IF NOT EXISTS "MatchPlayers" (
            "MatchId" INTEGER NOT NULL,
            "AccountId" INTEGER NOT NULL,
            "SteamId" INTEGER NOT NULL,
            "Team" INTEGER NOT NULL,
            "HeroId" INTEGER NOT NULL,
            "Won" INTEGER NOT NULL,
            "Gold" INTEGER NOT NULL,
            "Kills" INTEGER NOT NULL,
            "Deaths" INTEGER NOT NULL,
            "Assists" INTEGER NOT NULL,
            "LeaverStatus" INTEGER NOT NULL,
            "LastHits" INTEGER NOT NULL,
            "Denies" INTEGER NOT NULL,
            "GoldPerMin" INTEGER NOT NULL,
            "XpPerMinute" INTEGER NOT NULL,
            "GoldSpent" INTEGER NOT NULL,
            "Level" INTEGER NOT NULL,
            "ScaledHeroDamage" INTEGER NOT NULL,
            "ScaledTowerDamage" INTEGER NOT NULL,
            "ScaledHeroHealing" INTEGER NOT NULL,
            "TimeLastSeen" INTEGER NOT NULL,
            "SupportAbilityValue" INTEGER NOT NULL,
            "PartyId" INTEGER NOT NULL,
            "ClaimedFarmGold" INTEGER NOT NULL,
            "SupportGold" INTEGER NOT NULL,
            "ClaimedDenies" INTEGER NOT NULL,
            "ClaimedMisses" INTEGER NOT NULL,
            "Misses" INTEGER NOT NULL,
            "NetWorth" INTEGER NOT NULL,
            "HeroDamage" INTEGER NOT NULL,
            "TowerDamage" INTEGER NOT NULL,
            "HeroHealing" INTEGER NOT NULL,
            "MatchPlayerFlags" INTEGER NOT NULL,
            "HeroPickOrder" INTEGER NOT NULL,
            "HeroWasRandomed" INTEGER NOT NULL,
            "Lane" INTEGER NOT NULL,
            "ItemsJson" TEXT NOT NULL,
            "ItemPurchaseTimesJson" TEXT NOT NULL,
            CONSTRAINT "PK_MatchPlayers" PRIMARY KEY ("MatchId", "AccountId"),
            CONSTRAINT "FK_MatchPlayers_Matches_MatchId" FOREIGN KEY ("MatchId")
                REFERENCES "Matches" ("MatchId") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_MatchPlayers_AccountId" ON "MatchPlayers" ("AccountId");

        CREATE TABLE IF NOT EXISTS "PlayerProfileStats" (
            "AccountId" INTEGER NOT NULL CONSTRAINT "PK_PlayerProfileStats" PRIMARY KEY,
            "Games" INTEGER NOT NULL,
            "Wins" INTEGER NOT NULL,
            "Losses" INTEGER NOT NULL,
            "TotalKills" INTEGER NOT NULL,
            "TotalDeaths" INTEGER NOT NULL,
            "TotalAssists" INTEGER NOT NULL,
            "TotalLastHits" INTEGER NOT NULL,
            "TotalDenies" INTEGER NOT NULL,
            "TotalHeroDamage" INTEGER NOT NULL,
            "TotalTowerDamage" INTEGER NOT NULL,
            "TotalHeroHealing" INTEGER NOT NULL,
            "TotalGoldSpent" INTEGER NOT NULL,
            "TotalGoldPerMin" INTEGER NOT NULL,
            "TotalXpPerMinute" INTEGER NOT NULL,
            "TotalPlayTimeSeconds" INTEGER NOT NULL,
            "LeaverCount" INTEGER NOT NULL,
            "LastMatchAt" TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS "PlayerHeroStats" (
            "AccountId" INTEGER NOT NULL,
            "HeroId" INTEGER NOT NULL,
            "Games" INTEGER NOT NULL,
            "Wins" INTEGER NOT NULL,
            "Losses" INTEGER NOT NULL,
            "TotalKills" INTEGER NOT NULL,
            "TotalDeaths" INTEGER NOT NULL,
            "TotalAssists" INTEGER NOT NULL,
            "TotalLastHits" INTEGER NOT NULL,
            "TotalDenies" INTEGER NOT NULL,
            "TotalHeroDamage" INTEGER NOT NULL,
            "TotalTowerDamage" INTEGER NOT NULL,
            "TotalHeroHealing" INTEGER NOT NULL,
            "TotalGoldSpent" INTEGER NOT NULL,
            "TotalGoldPerMin" INTEGER NOT NULL,
            "TotalXpPerMinute" INTEGER NOT NULL,
            "LastMatchAt" TEXT NULL,
            CONSTRAINT "PK_PlayerHeroStats" PRIMARY KEY ("AccountId", "HeroId")
        );
        CREATE INDEX IF NOT EXISTS "IX_PlayerHeroStats_AccountId" ON "PlayerHeroStats" ("AccountId");

        CREATE TABLE IF NOT EXISTS "ProfileCards" (
            "AccountId" INTEGER NOT NULL CONSTRAINT "PK_ProfileCards" PRIMARY KEY,
            "SlotsJson" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL
        );

        """);

        D2stDatabaseMigrator.MarkLegacyBaseline(db);
        // Apply migrations added after the legacy baseline in this same
        // startup, so the new store is available immediately after upgrade.
        db.Database.Migrate();
    }
}

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapUserEndpoints();
app.MapFriendEndpoints();
app.MapLobbyEndpoints();
app.MapNetworkEndpoints();
app.MapAuthTicketEndpoints();
app.MapGameServerEndpoints();
app.MapStorageEndpoints();
app.MapStatsEndpoints();
app.MapStoreEndpoints();
app.MapDotaPlusEndpoints();
app.MapLeaderboardEndpoints();
app.MapWorkshopEndpoints();
app.MapEventEndpoints();
app.MapGameCoordinatorEndpoints();

app.Run();

static void EnsureSqliteDirectory(string connectionString)
{
    var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
    var directory = Path.GetDirectoryName(dataSource);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

static bool HasPlayerRankCalibrationColumn(D2stDbContext db) =>
    HasColumn(db, "PlayerRanks", "IsCalibrated");

static bool HasColumn(D2stDbContext db, string tableName, string columnName)
{
    var connection = db.Database.GetDbConnection();
    var openedHere = connection.State != System.Data.ConnectionState.Open;
    if (openedHere)
    {
        connection.Open();
    }

    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(1) &&
                string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
    finally
    {
        if (openedHere)
        {
            connection.Close();
        }
    }
}

// Exposed so WebApplicationFactory-based tests can reference the entry point.
public partial class Program;
