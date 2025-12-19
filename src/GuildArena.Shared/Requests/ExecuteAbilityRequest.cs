using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.Requests;

/// <summary>
/// Represents the client's intent to execute a specific ability in a combat session.
/// </summary>
public class ExecuteAbilityRequest
{
    /// <summary>
    /// The ID of the combat session (GUID).
    /// </summary>
    public required string CombatId { get; set; }

    /// <summary>
    /// The ID of the hero attempting to cast the ability.
    /// Explicitly required to validate ownership.
    /// </summary>
    public int SourceId { get; set; }

    /// <summary>
    /// The Definition ID of the ability to cast (e.g., "ABIL_FIREBALL").
    /// </summary>
    public required string AbilityId { get; set; }

    /// <summary>
    /// The selected targets for the ability.
    /// Key: The RuleId defined in the Ability JSON.
    /// Value: List of Combatant IDs selected for that rule.
    /// </summary>
    public Dictionary<string, List<int>> TargetSelections { get; set; } = new();

    /// <summary>
    /// The manual allocation of essence to pay for the ability costs.
    /// Key: Essence Type.
    /// Value: Amount to spend.
    /// </summary>
    public Dictionary<EssenceType, int> Payment { get; set; } = new();
}