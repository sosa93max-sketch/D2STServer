using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D2ST.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PersonaName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Avatar = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FromAccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ToAccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    FriendAccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => new { x.AccountId, x.FriendAccountId });
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                columns: table => new
                {
                    LeaderboardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    UgcHandle = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => new { x.LeaderboardId, x.AccountId });
                });

            migrationBuilder.CreateTable(
                name: "Leaderboards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SortMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leaderboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    MatchId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LobbyId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<uint>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<uint>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GoodGuysWin = table.Column<bool>(type: "INTEGER", nullable: false),
                    WinningTeam = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstBloodTime = table.Column<uint>(type: "INTEGER", nullable: false),
                    RadiantScore = table.Column<uint>(type: "INTEGER", nullable: false),
                    DireScore = table.Column<uint>(type: "INTEGER", nullable: false),
                    TowerStatusJson = table.Column<string>(type: "TEXT", nullable: false),
                    BarracksStatusJson = table.Column<string>(type: "TEXT", nullable: false),
                    TeamScoresJson = table.Column<string>(type: "TEXT", nullable: false),
                    Cluster = table.Column<uint>(type: "INTEGER", nullable: false),
                    ServerAddress = table.Column<string>(type: "TEXT", nullable: false),
                    EventScore = table.Column<uint>(type: "INTEGER", nullable: false),
                    AutomaticSurrender = table.Column<bool>(type: "INTEGER", nullable: false),
                    ServerVersion = table.Column<uint>(type: "INTEGER", nullable: false),
                    PreGameDuration = table.Column<uint>(type: "INTEGER", nullable: false),
                    AverageNetworthDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchFlags = table.Column<uint>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.MatchId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerHeroStats",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalKills = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalDeaths = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalAssists = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalLastHits = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalDenies = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalHeroDamage = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalTowerDamage = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalHeroHealing = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalGoldSpent = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalGoldPerMin = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalXpPerMinute = table.Column<long>(type: "INTEGER", nullable: false),
                    LastMatchAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerHeroStats", x => new { x.AccountId, x.HeroId });
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfileStats",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Games = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalKills = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalDeaths = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalAssists = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalLastHits = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalDenies = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalHeroDamage = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalTowerDamage = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalHeroHealing = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalGoldSpent = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalGoldPerMin = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalXpPerMinute = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalPlayTimeSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    LeaverCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastMatchAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfileStats", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerRanks",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mmr = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    Games = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCalibrated = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRanks", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "ProfileCards",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SlotsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileCards", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "RemoteStorageFiles",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SyncPlatforms = table.Column<uint>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteStorageFiles", x => new { x.AccountId, x.FileName });
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Earned = table.Column<bool>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Progress = table.Column<uint>(type: "INTEGER", nullable: false),
                    MaxProgress = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => new { x.AccountId, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "UserStats",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Data = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStats", x => new { x.AccountId, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "WorkshopItems",
                columns: table => new
                {
                    PublishedFileId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatorAppId = table.Column<uint>(type: "INTEGER", nullable: false),
                    ConsumerAppId = table.Column<uint>(type: "INTEGER", nullable: false),
                    OwnerSteamId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    FileType = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    PreviewUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    Banned = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcceptedForUse = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeCreated = table.Column<uint>(type: "INTEGER", nullable: false),
                    TimeUpdated = table.Column<uint>(type: "INTEGER", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalFilesSize = table.Column<long>(type: "INTEGER", nullable: false),
                    VotesUp = table.Column<uint>(type: "INTEGER", nullable: false),
                    VotesDown = table.Column<uint>(type: "INTEGER", nullable: false),
                    Score = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkshopItems", x => x.PublishedFileId);
                });

            migrationBuilder.CreateTable(
                name: "WorkshopSubscriptions",
                columns: table => new
                {
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    PublishedFileId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    SubscribedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DisabledLocally = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkshopSubscriptions", x => new { x.AccountId, x.PublishedFileId });
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayers",
                columns: table => new
                {
                    MatchId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<uint>(type: "INTEGER", nullable: false),
                    SteamId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Team = table.Column<int>(type: "INTEGER", nullable: false),
                    HeroId = table.Column<int>(type: "INTEGER", nullable: false),
                    Won = table.Column<bool>(type: "INTEGER", nullable: false),
                    Gold = table.Column<uint>(type: "INTEGER", nullable: false),
                    Kills = table.Column<uint>(type: "INTEGER", nullable: false),
                    Deaths = table.Column<uint>(type: "INTEGER", nullable: false),
                    Assists = table.Column<uint>(type: "INTEGER", nullable: false),
                    LeaverStatus = table.Column<uint>(type: "INTEGER", nullable: false),
                    LastHits = table.Column<uint>(type: "INTEGER", nullable: false),
                    Denies = table.Column<uint>(type: "INTEGER", nullable: false),
                    GoldPerMin = table.Column<uint>(type: "INTEGER", nullable: false),
                    XpPerMinute = table.Column<uint>(type: "INTEGER", nullable: false),
                    GoldSpent = table.Column<uint>(type: "INTEGER", nullable: false),
                    Level = table.Column<uint>(type: "INTEGER", nullable: false),
                    ScaledHeroDamage = table.Column<uint>(type: "INTEGER", nullable: false),
                    ScaledTowerDamage = table.Column<uint>(type: "INTEGER", nullable: false),
                    ScaledHeroHealing = table.Column<uint>(type: "INTEGER", nullable: false),
                    TimeLastSeen = table.Column<uint>(type: "INTEGER", nullable: false),
                    SupportAbilityValue = table.Column<uint>(type: "INTEGER", nullable: false),
                    PartyId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ClaimedFarmGold = table.Column<uint>(type: "INTEGER", nullable: false),
                    SupportGold = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClaimedDenies = table.Column<uint>(type: "INTEGER", nullable: false),
                    ClaimedMisses = table.Column<uint>(type: "INTEGER", nullable: false),
                    Misses = table.Column<uint>(type: "INTEGER", nullable: false),
                    NetWorth = table.Column<uint>(type: "INTEGER", nullable: false),
                    HeroDamage = table.Column<uint>(type: "INTEGER", nullable: false),
                    TowerDamage = table.Column<uint>(type: "INTEGER", nullable: false),
                    HeroHealing = table.Column<uint>(type: "INTEGER", nullable: false),
                    MatchPlayerFlags = table.Column<uint>(type: "INTEGER", nullable: false),
                    HeroPickOrder = table.Column<uint>(type: "INTEGER", nullable: false),
                    HeroWasRandomed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Lane = table.Column<uint>(type: "INTEGER", nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ItemPurchaseTimesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayers", x => new { x.MatchId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_MatchPlayers_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "MatchId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_FromAccountId_Status",
                table: "FriendRequests",
                columns: new[] { "FromAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_ToAccountId_Status",
                table: "FriendRequests",
                columns: new[] { "ToAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Leaderboards_AppId_Name",
                table: "Leaderboards",
                columns: new[] { "AppId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_EndedAt",
                table: "Matches",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_LobbyId",
                table: "Matches",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_AccountId",
                table: "MatchPlayers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHeroStats_AccountId",
                table: "PlayerHeroStats",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "LeaderboardEntries");

            migrationBuilder.DropTable(
                name: "Leaderboards");

            migrationBuilder.DropTable(
                name: "MatchPlayers");

            migrationBuilder.DropTable(
                name: "PlayerHeroStats");

            migrationBuilder.DropTable(
                name: "PlayerProfileStats");

            migrationBuilder.DropTable(
                name: "PlayerRanks");

            migrationBuilder.DropTable(
                name: "ProfileCards");

            migrationBuilder.DropTable(
                name: "RemoteStorageFiles");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserStats");

            migrationBuilder.DropTable(
                name: "WorkshopItems");

            migrationBuilder.DropTable(
                name: "WorkshopSubscriptions");

            migrationBuilder.DropTable(
                name: "Matches");
        }
    }
}
