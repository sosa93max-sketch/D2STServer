using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

/// <summary>Persisted player account. AccountId is the 32-bit Steam account id.</summary>
public sealed class AccountEntity
{
    [Key]
    public uint AccountId { get; set; }

    [MaxLength(64)]
    public required string Username { get; set; }

    public required byte[] PasswordHash { get; set; }

    public required byte[] PasswordSalt { get; set; }

    /// <summary>
    /// Display name the client advertises. It is separate from
    /// <see cref="Username"/>, which is the unique login handle.
    /// </summary>
    [MaxLength(128)]
    public string? PersonaName { get; set; }

    /// <summary>Avatar bytes as uploaded by the client (PNG).</summary>
    public byte[]? Avatar { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
