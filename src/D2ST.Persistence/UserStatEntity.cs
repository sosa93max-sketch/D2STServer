using System.ComponentModel.DataAnnotations;

namespace D2ST.Persistence;

public sealed class UserStatEntity
{
    public uint AccountId { get; set; }

    [MaxLength(128)]
    public required string Name { get; set; }

    public uint Data { get; set; }
}
