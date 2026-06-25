using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Combat.Reconnection;

/// <summary>
/// Handles the retrieval of the full combat state for a reconnecting player.
/// Returns the Domain GameState.
/// </summary>
public class GetReconnectedCombatStateQueryHandler : IRequestHandler<GetReconnectedCombatStateQuery, Result<StartCombatResult>>
{
    private readonly ICombatStateRepository _combatRepo;

    public GetReconnectedCombatStateQueryHandler(ICombatStateRepository combatRepo)
    {
        _combatRepo = combatRepo;
    }

    public async Task<Result<StartCombatResult>> Handle(GetReconnectedCombatStateQuery request, CancellationToken cancellationToken)
    {
        var gameState = await _combatRepo.GetAsync(request.CombatId);

        if (gameState == null)
        {
            return Result.Failure<StartCombatResult>(new Error(
                "Combat.NotFound", "The requested combat session has expired or does not exist.", ErrorType.NotFound));
        }

        return new StartCombatResult
        {
            CombatId = request.CombatId,
            InitialLogs = new List<string> { "--- Reconnected to active combat ---" },
            InitialState = gameState
        };
    }
}