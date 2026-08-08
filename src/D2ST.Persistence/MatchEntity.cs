using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>One completed match reported by a local lobby game server.</summary>
public sealed class MatchEntity
{
    [Key]
    public ulong MatchId { get; set; }

    public ulong LobbyId { get; set; }

    public uint GameMode { get; set; }

    public uint DurationSeconds { get; set; }

    public DateTimeOffset EndedAt { get; set; }

    public bool GoodGuysWin { get; set; }

    public int WinningTeam { get; set; }

    public uint FirstBloodTime { get; set; }

    public uint RadiantScore { get; set; }

    public uint DireScore { get; set; }

    public string TowerStatusJson { get; set; } = "[]";

    public string BarracksStatusJson { get; set; } = "[]";

    public string TeamScoresJson { get; set; } = "[]";

    public uint Cluster { get; set; }

    public string ServerAddress { get; set; } = string.Empty;

    public uint EventScore { get; set; }

    public bool AutomaticSurrender { get; set; }

    public uint ServerVersion { get; set; }

    public uint PreGameDuration { get; set; }

    public int AverageNetworthDelta { get; set; }

    public uint MatchFlags { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<MatchPlayerEntity> Players { get; set; } = new List<MatchPlayerEntity>();
}
