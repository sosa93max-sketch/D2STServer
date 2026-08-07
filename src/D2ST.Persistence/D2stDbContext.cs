using Microsoft.EntityFrameworkCore;

namespace D2ST.Persistence;

public sealed class D2stDbContext : DbContext
{
    public D2stDbContext(DbContextOptions<D2stDbContext> options)
        : base(options)
    {
    }

    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    public DbSet<FriendshipEntity> Friendships => Set<FriendshipEntity>();

    public DbSet<FriendRequestEntity> FriendRequests => Set<FriendRequestEntity>();

    public DbSet<RemoteStorageFileEntity> RemoteStorageFiles => Set<RemoteStorageFileEntity>();

    public DbSet<UserStatEntity> UserStats => Set<UserStatEntity>();

    public DbSet<UserAchievementEntity> UserAchievements => Set<UserAchievementEntity>();

    public DbSet<LeaderboardEntity> Leaderboards => Set<LeaderboardEntity>();

    public DbSet<LeaderboardEntryEntity> LeaderboardEntries => Set<LeaderboardEntryEntity>();

    public DbSet<WorkshopItemEntity> WorkshopItems => Set<WorkshopItemEntity>();

    public DbSet<WorkshopSubscriptionEntity> WorkshopSubscriptions => Set<WorkshopSubscriptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>()
            .HasIndex(account => account.Username)
            .IsUnique();

        modelBuilder.Entity<FriendshipEntity>()
            .HasKey(friendship => new { friendship.AccountId, friendship.FriendAccountId });

        modelBuilder.Entity<FriendRequestEntity>()
            .HasIndex(request => new { request.ToAccountId, request.Status });

        modelBuilder.Entity<FriendRequestEntity>()
            .HasIndex(request => new { request.FromAccountId, request.Status });

        modelBuilder.Entity<RemoteStorageFileEntity>()
            .HasKey(file => new { file.AccountId, file.FileName });

        modelBuilder.Entity<UserStatEntity>()
            .HasKey(stat => new { stat.AccountId, stat.Name });

        modelBuilder.Entity<UserAchievementEntity>()
            .HasKey(achievement => new { achievement.AccountId, achievement.Name });

        modelBuilder.Entity<LeaderboardEntity>()
            .HasIndex(leaderboard => new { leaderboard.AppId, leaderboard.Name })
            .IsUnique();

        modelBuilder.Entity<LeaderboardEntryEntity>()
            .HasKey(entry => new { entry.LeaderboardId, entry.AccountId });

        modelBuilder.Entity<WorkshopSubscriptionEntity>()
            .HasKey(subscription => new { subscription.AccountId, subscription.PublishedFileId });
    }
}
