namespace GuildArena.Domain.ValueObjects;

public class ActiveCooldown
{
    public required string AbilityId { get; set; }
    public int TurnsRemaining { get; set; }
}
