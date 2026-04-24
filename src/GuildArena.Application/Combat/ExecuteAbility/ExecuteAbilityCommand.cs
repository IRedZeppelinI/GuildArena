using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Combat.ExecuteAbility;

/// <summary>
/// Command to execute an ability within a combat session.
/// </summary>
public class ExecuteAbilityCommand : IRequest<Result>
{
    public required string CombatId { get; set; }
    public int SourceId { get; set; }
    public required string AbilityId { get; set; }
    public Dictionary<string, List<int>> TargetSelections { get; set; } = new();
    public Dictionary<EssenceType, int> Payment { get; set; } = new();
}