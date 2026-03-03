using GuildArena.Domain.Enums.Modifiers;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Represents a buff or debuff currently active on a combatant.
/// </summary>
public class ActiveModifierDto
{
    public required string DefinitionId { get; set; }

    /// <summary>
    /// How many turns this modifier will last. -1 indicates permanent/passive.
    /// </summary>
    public int TurnsRemaining { get; set; }

    public int StackCount { get; set; }
    public float CurrentBarrierValue { get; set; }

    /// <summary>
    /// List of visual status effects (e.g., Stun, Silence) for UI icons.
    /// </summary>
    public List<StatusEffectType> ActiveStatusEffects { get; set; } = new();
}