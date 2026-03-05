using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Application.Combat.AI;

/// <summary>
/// Represents a valid, executable decision made by the AI.
/// It mirrors the data a human player would send via the API.
/// </summary>
public class AiActionIntent
{
    public int SourceId { get; set; }
    public required string AbilityId { get; set; }

    /// <summary>
    /// The targets chosen by the AI.
    /// Key: RuleId. Value: List of target Combatant Ids.
    /// </summary>
    public Dictionary<string, List<int>> TargetSelections { get; set; } = new();

    /// <summary>
    /// How the AI decided to allocate its essence to pay for this ability.
    /// </summary>
    public Dictionary<EssenceType, int> Payment { get; set; } = new();
}