using MediatR;

namespace GuildArena.Application.Combat.StartCombat;

/// <summary>
/// Internal command to initialize a PvE combat based on persistent player data and static encounter definitions.
/// </summary>
public class StartPveCombatCommand : IRequest<string>
{
    public required string EncounterId { get; set; }
    public List<int> HeroInstanceIds { get; set; } = new();
}