namespace GuildArena.Domain.ValueObjects;

/// <summary>
/// Defines a modification to the HP cost of abilities.
/// </summary>
public class HPCostModification
{
    /// <summary>
    /// If populated, applies only to abilities with this tag.
    /// </summary>
    public string? RequiredAbilityTag { get; set; }

    /// <summary>
    /// The value to add (penalty) or subtract (discount) from the HP cost.
    /// </summary>
    public int Value { get; set; }
}