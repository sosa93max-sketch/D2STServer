using D2ST.Api;
using D2ST.Api.Endpoints;
using D2ST.Api.Logging;
using D2ST.GameCoordinator;
using D2ST.GameCoordinator.Messaging;
using D2ST.GameCoordinator.Players;
using D2ST.Persistence;
using D2ST.Steam;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("D2st") ?? "Data Source=Data/d2st.db";
EnsureSqliteDirectory(connectionString);

builder.Logging.AddFileLogger(builder.Configuration);

builder.Services.AddD2stPersistence(connectionString);
builder.Services.AddSteamServices(builder.Configuration);
builder.Services.AddSingleton<IGcPlayerDirectory, SessionGcPlayerDirectory>();
builder.Services.AddSingleton<IGcMessageQueue, EventStreamGcMessageQueue>();
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
