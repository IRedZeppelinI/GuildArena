using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Entities;

/// <summary>
/// Represents a "player" (human or AI) within a combat instance.
/// Holds player-level state like Essence.
/// </summary>
public class CombatPlayer
{
    /// <summary>
    /// ID from player (correspondes to Combatant.OwnerId).
    /// </summary>
    public int PlayerId { get; set; }

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