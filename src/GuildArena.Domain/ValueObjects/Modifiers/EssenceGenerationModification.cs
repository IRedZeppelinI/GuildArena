using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Defines a modification to the passive essence generation at the start of a turn.
/// </summary>
public class EssenceGenerationModification
{
    /// <summary>
    /// If true, a random essence type will be selected (for gain or loss).
    /// If true, EssenceType property is ignored.
    /// </summary>
    public bool IsRandom { get; set; }

    /// <summary>
    /// The type of essence to generate extra (or remove).
    /// </summary>
    public EssenceType EssenceType { get; set; }

    /// <summary>
    /// The amount to add (positive) or remove (negative) per turn.
    /// </summary>
    public int Amount { get; set; }
}