namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Defines a modification to the attacker's hit chance (Accuracy).
/// Positive values increase accuracy; negative values decrease it (e.g. Blind).
/// </summary>
public class HitChanceModification
{
    /// <summary>
    /// The value to add to the hit chance percentage (e.g. -0.20 for -20%).
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// If populated, this modification only applies to abilities with this specific tag.
    /// Ex: "Ranged" (Eagle Eye only helps ranged attacks).
    /// </summary>
    public string? RequiredAbilityTag { get; set; }
}