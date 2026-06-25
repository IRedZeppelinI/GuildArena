using GuildArena.Application.Combat.StartCombat;
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Combat.Reconnection;

/// <summary>
/// Query to fetch the full game state of an ongoing combat session.
/// </summary>
public class GetReconnectedCombatStateQuery : IRequest<Result<StartCombatResult>>
{
    public required string CombatId { get; set; }
}