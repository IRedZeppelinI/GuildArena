using GuildArena.Domain.Enums.Combat;

namespace GuildArena.Domain.Gameplay;

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

    public string BackgroundId { get; set; } = string.Empty;


    /// <summary>
    /// Current combat resolution status.
    /// </summary>
    public CombatStatus Status { get; set; } = CombatStatus.Ongoing;

    /// <summary>
    /// The type of match being played (Encounter, Dungeon, PvP).
    /// </summary>
    public GuildArena.Domain.Enums.Matches.MatchType MatchType { get; set; }

    /// <summary>
    /// Context identifier (e.g., the EncounterId for PvE fights).
    /// </summary>
    public string ContextId { get; set; } = string.Empty;
}