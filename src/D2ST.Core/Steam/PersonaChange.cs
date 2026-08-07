namespace D2ST.Core.Steam;

/// <summary>
/// Steamworks EPersonaChange flags carried on persona/presence events. The game
/// uses them to decide what to re-read (name, avatar, rich presence, ...).
/// </summary>
[Flags]
public enum PersonaChange
{
    None = 0,
    Name = 1,
    Status = 2,
    Avatar = 64,
    Relationship = 512,
    RichPresence = 2048
}
