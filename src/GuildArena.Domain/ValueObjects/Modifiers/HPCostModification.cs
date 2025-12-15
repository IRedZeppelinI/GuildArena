namespace GuildArena.Domain.ValueObjects.Modifiers;

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
    /// If populated, this modification only applies if the ability targets units of this specific Race ID.
    /// <para>
    /// Logic: 
    /// If Value > 0 (Penalty): Applies if ANY manual target matches the race.
    /// If Value < 0 (Discount): Applies only if ALL manual targets match the race.
    /// </para>
    /// </summary>
    public string? TargetRaceId { get; set; }

    /// <summary>
    /// The value to add (penalty) or subtract (discount) from the HP cost.
    /// </summary>
    public int Value { get; set; }
}