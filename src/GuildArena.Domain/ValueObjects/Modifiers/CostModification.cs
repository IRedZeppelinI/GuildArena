using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Defines a rule for modifying the essence cost of abilities based on tags or essence types.
/// </summary>
public class CostModification
{
    /// <summary>
    /// The specific essence type cost to modify.
    /// If null, it may apply to Neutral costs or any cost depending on implementation.
    /// </summary>
    public EssenceType? TargetEssenceType { get; set; }

    /// <summary>
    /// If populated, this modification only applies to abilities containing this tag.
    /// If null, it applies to ALL abilities.
    /// </summary>
    public string? RequiredAbilityTag { get; set; }

    /// <summary>
    /// If populated, this modification only applies if the ability 
    /// targets a unit of this specific Race ID.
    /// </summary>
    public string? TargetRaceId { get; set; }

    /// <summary>
    /// The value to add (positive) or subtract (negative) from the cost.
    /// </summary>
    public int Value { get; set; }
}