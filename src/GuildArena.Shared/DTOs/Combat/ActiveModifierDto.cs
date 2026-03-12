using GuildArena.Domain.Enums.Modifiers;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Represents a buff or debuff currently active on a combatant.
/// </summary>
public class ActiveModifierDto
{
    public required string DefinitionId { get; set; }

    // NOVO: Necessário para a UI saber quem aplicou o Taunt, ou outras mecânicas de "Link"
    public int CasterId { get; set; }

    public int TurnsRemaining { get; set; }
    public int StackCount { get; set; }
    public float CurrentBarrierValue { get; set; }

    public List<StatusEffectType> ActiveStatusEffects { get; set; } = new();
}