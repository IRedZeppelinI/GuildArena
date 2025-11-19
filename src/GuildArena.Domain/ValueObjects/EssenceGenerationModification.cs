using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects;

/// <summary>
/// Defines a modification to the passive essence generation at the start of a turn.
/// </summary>
public class EssenceGenerationModification
{
    /// <summary>
    /// The type of essence to generate extra (or remove).
    /// </summary>
    public EssenceType EssenceType { get; set; }

    /// <summary>
    /// The amount to add (positive) or remove (negative) per turn.
    /// </summary>
    public int Amount { get; set; }
}