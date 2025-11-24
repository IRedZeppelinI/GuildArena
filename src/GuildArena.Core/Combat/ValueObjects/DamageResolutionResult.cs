namespace GuildArena.Core.Combat.ValueObjects;

/// <summary>
/// Represents the detailed result of a damage calculation process.
/// </summary>
public class DamageResolutionResult
{
    /// <summary>
    /// The final damage amount that should be subtracted from the target's HP.
    /// </summary>
    public float FinalDamageToApply { get; set; }

    /// <summary>
    /// The amount of damage that was absorbed by barriers (useful for logs/UI).
    /// </summary>
    public float AbsorbedDamage { get; set; }

    /// <summary>
    /// Indicates if the damage was completely negated (either by mitigation or absorption).
    /// </summary>
    public bool IsFullyMitigated => FinalDamageToApply <= 0;
}