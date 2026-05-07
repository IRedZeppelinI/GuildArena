namespace GuildArena.Shared.Requests;

/// <summary>
/// Payload to request the creation of a new player Guild.
/// </summary>
public class CreateGuildRequest
{
    public required string GuildName { get; set; }
}