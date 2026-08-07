using D2ST.Api;
using D2ST.Api.Endpoints;
using D2ST.Api.Logging;
using D2ST.Api.Ranks;
using D2ST.GameCoordinator;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Players;
using D2ST.GameCoordinator.Ranks;
using D2ST.Persistence;
using D2ST.Steam;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("D2st") ?? "Data Source=Data/d2st.db";
EnsureSqliteDirectory(connectionString);

builder.Logging.AddFileLogger(builder.Configuration);

builder.Services.AddD2stPersistence(connectionString);
builder.Services.AddSteamServices(builder.Configuration);
builder.Services.AddSingleton<IGcPlayerDirectory, SessionGcPlayerDirectory>();
builder.Services.AddSingleton<IGcMessageQueue, EventStreamGcMessageQueue>();
builder.Services.AddSingleton<IRankStore, RankStore>();
builder.Services.AddGameCoordinator(builder.Configuration, builder.Environment.ContentRootPath);

// The shim serializes/deserializes with PascalCase member names, so keep the
// property names verbatim instead of camel-casing them.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// Scaffold-stage schema bootstrap. Replace with EF Core migrations before any
// data needs to survive a schema change.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
    db.Database.EnsureCreated();

    // EnsureCreated() only creates tables on a brand-new database, so tables
    // added later are created by hand here until stage 5 introduces migrations.
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

    // Existing databases were created before calibration was persisted. Keep
    // their positive MMR assignments visible, then leave new/zero-MMR users
    // uncalibrated until an admin assignment or rated result marks them so.
    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE \"PlayerRanks\" ADD COLUMN \"IsCalibrated\" INTEGER NOT NULL DEFAULT 0;");
    }
    catch (SqliteException error) when (
        error.SqliteErrorCode == 1 &&
        error.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
    {
        // The column is already present on a newly created or previously
        // upgraded database.
    }

    db.Database.ExecuteSqlRaw(
        "UPDATE \"PlayerRanks\" SET \"IsCalibrated\" = 1 WHERE \"Mmr\" > 0 AND \"IsCalibrated\" = 0;");
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

// Exposed so WebApplicationFactory-based tests can reference the entry point.
public partial class Program;
