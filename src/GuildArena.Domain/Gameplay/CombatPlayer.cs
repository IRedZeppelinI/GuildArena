using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.State;

namespace GuildArena.Domain.Gameplay;

/// <summary>
/// Represents a "player" (human or AI) within a combat instance.
/// Holds player-level state like Essence.
/// </summary>
public class CombatPlayer
{
    /// <summary>
    /// In-match ID assigned by the combat engine (e.g., 1 for Player 1, 2 for Player 2, -1 for AI).
    /// Corresponds to Combatant.OwnerId.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The Global Identity GUID of the user controlling this seat. Null if AI.
    /// Used by the API to authorize requests.
    /// </summary>
    public string? UserId { get; set; }

    public string Name { get; set; } = "Unknown Player";

    /// <summary>
    /// Type of Player (Human or AI).
    /// </summary>
    public CombatPlayerType Type { get; set; }


    /// <summary>
    /// The current pool of essence available to the player, organized by type.
    /// </summary>   
    public Dictionary<EssenceType, int> EssencePool { get; set; } = new();

    /// <summary>
    /// The maximum total essence the player can hold (optional cap).
    /// </summary>
    public int MaxTotalEssence { get; set; } = 20;

    /// <summary>
    /// Modifiers ativos globais no jogador (ex: "Mana Spring", "Cursed wallet").
    /// </summary>
    public List<ActiveModifier> ActiveModifiers { get; set; } = new();
}