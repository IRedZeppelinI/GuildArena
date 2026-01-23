namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Defines a modification to the defender's evasion chance.
/// Positive values increase evasion (e.g. Blur).
/// </summary>
public class EvasionModification
{
    /// <summary>
    /// The value to add to the evasion percentage (e.g. 0.15 for +15%).
    /// </summary>
    public float Value { get; set; }

    /// <summary>
    /// If populated, evasion only applies against damage of this specific category.
    /// Ex: "Physical" (Blur works against swords but not spells).
    /// </summary>
    public string? RequiredDamageCategory { get; set; }
}