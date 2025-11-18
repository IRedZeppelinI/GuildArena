using GuildArena.Domain.Enums;

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


    // Essence placeholder (A lógica/tipo de Essence será definida no futuro)    
    public int CurrentEssence { get; set; }    
    public int MaxEssence { get; set; }
}