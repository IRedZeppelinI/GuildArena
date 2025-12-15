using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Describes a rule for modifying the base cooldown of an ability,
/// usually read from a ModifierDefinition.
/// </summary>
public class CooldownModification
{
    /// <summary>
    /// If populated, this modification only applies to abilities
    /// that contain this tag. If null, it applies to all.
    /// </summary>
    public string? RequiredTag { get; set; }

    /// <summary>
    /// The type of modification (FLAT or PERCENTAGE).
    /// </summary>
    public ModificationType Type { get; set; }

    /// <summary>
    /// The value (e.g., -1 for FLAT, -0.1f for PERCENTAGE).
    /// </summary>
    public float Value { get; set; }
}