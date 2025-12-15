using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects;

/// <summary>
/// Represents a modification to a final damage calculation,
/// usually triggered by a tag.
/// </summary>
public class DamageModification
{
    /// <summary>
    /// The tag that this modification applies to (e.g., "Nature", "Physical", "Melee").
    /// </small>
    public required string RequiredTag { get; set; }

    /// <summary>
    /// If populated, this modification only applies if the target belongs to this specific Race ID.
    /// </summary>
    public string? TargetRaceId { get; set; }

    public ModificationType Type { get; set; } // FLAT ou PERCENTAGE
    public float Value { get; set; }
}