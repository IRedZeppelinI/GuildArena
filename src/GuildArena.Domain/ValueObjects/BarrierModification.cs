using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects;

/// <summary>
/// Defines a modification to the strength of barriers created by the character.
/// </summary>
public class BarrierModification
{
    /// <summary>
    /// The type of modification (Flat or Percentage).
    /// </summary>
    public ModificationType Type { get; set; }

    /// <summary>
    /// The value to add (Flat) or the percentage to increase (Percentage).
    /// </summary>
    public float Value { get; set; }
}