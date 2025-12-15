using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Domain.ValueObjects.Resources;

/// <summary>
/// Represents a specific amount of a certain essence type required as a cost.
/// </summary>
public class EssenceAmount
{
    /// <summary>
    /// The type of essence required.
    /// </summary>
    public EssenceType Type { get; set; }
    /// <summary>
    /// The amount required.
    /// </summary>
    public int Amount { get; set; }
}