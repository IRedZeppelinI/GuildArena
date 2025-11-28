using GuildArena.Domain.ValueObjects;

namespace GuildArena.Core.Combat.ValueObjects;

/// <summary>
/// Represents the total calculated cost (Invoice) required to execute an ability,
/// including both Essence and HP components.
/// </summary>
public class FinalAbilityCosts
{
    /// <summary>
    /// The list of essence required, consolidated by type.
    /// </summary>
    public List<EssenceAmount> EssenceCosts { get; set; } = new();

    /// <summary>
    /// The total HP cost required (sacrificial cost + blood wards).
    /// </summary>
    public int HPCost { get; set; }
}