using GuildArena.Domain.Enums.Modifiers;

namespace GuildArena.Domain.ValueObjects.State;

public class ActiveModifier
{
    public required string DefinitionId { get; set; } // "MOD_POISON_WEAK"
    public int TurnsRemaining { get; set; } // Ex: 3

   
    public int CasterId { get; set; }

    /// <summary>
    /// The current number of stacks for this modifier.
    /// Affects the magnitude of Stat Modifications.
    /// Default starts at 1.
    /// </summary>
    public int StackCount { get; set; } = 1;

    /// <summary>
    /// The remaining health of the barrier provided by this modifier.
    /// </summary>
    public float CurrentBarrierValue { get; set; }


    /// <summary>
    /// Snapshot of status effects granted by this modifier.    
    /// </summary>
    public List<StatusEffectType> ActiveStatusEffects { get; set; } = new();


}
