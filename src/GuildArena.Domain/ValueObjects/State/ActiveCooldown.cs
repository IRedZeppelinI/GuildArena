namespace GuildArena.Domain.ValueObjects.State;

public class ActiveCooldown
{
    public required string AbilityId { get; set; }
    public int TurnsRemaining { get; set; }
}
