using GuildArena.Domain.Enums.Resources;
using MediatR;

namespace GuildArena.Application.Combat.ExecuteAbility;

/// <summary>
/// Command to execute an ability within a combat session.
/// </summary>
public class ExecuteAbilityCommand : IRequest
{
    public required string CombatId { get; set; }

    /// <summary>
    /// The ID of the combatant performing the action.
    /// Used to validate if the player owns this combatant.
    /// </summary>
    public int SourceId { get; set; }

    public required string AbilityId { get; set; }

    /// <summary>
    /// Mapping of RuleId -> List of Target IDs.
    /// </summary>
    public Dictionary<string, List<int>> TargetSelections { get; set; } = new();

    /// <summary>
    /// Resources allocated by the player to pay for the ability.
    /// </summary>
    public Dictionary<EssenceType, int> Payment { get; set; } = new();
}