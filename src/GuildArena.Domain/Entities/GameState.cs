namespace GuildArena.Domain.Entities;

public class GameState
{
    /// <summary>
    /// List of all combatants on the combat battlefield.
    /// </summary>
    public List<Combatant> Combatants { get; set; } = new();

    /// <summary>
    /// List of all Playes on combat (human/AI).
    /// </summary>
    public List<CombatPlayer> Players { get; set; } = new(); 

    /// <summary>
    /// Current turn number.
    /// </summary>
    public int CurrentTurnNumber { get; set; } = 1;

    /// <summary>
    /// Current turn Player Id
    /// </summary>
    public int CurrentPlayerId { get; set; }
}