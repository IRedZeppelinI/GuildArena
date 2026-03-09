using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.Combat.StartCombat;

/// <summary>
/// Encapsulates the result of initializing a new combat session.
/// </summary>
public class StartCombatResult
{
    public required string CombatId { get; set; }
    public required List<string> InitialLogs { get; set; }

    public required GameState InitialState { get; set; }
}