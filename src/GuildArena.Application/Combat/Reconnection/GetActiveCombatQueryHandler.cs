using GuildArena.Application.Abstractions;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Combat;
using MediatR;

namespace GuildArena.Application.Combat.Reconnection;

/// <summary>
/// Handles the retrieval of the active combat pointer for the current user.
/// Validates if the pointer is stale (e.g., combat expired in Redis) and cleans it up if necessary.
/// </summary>
public class GetActiveCombatQueryHandler : IRequestHandler<GetActiveCombatQuery, Result<ActiveCombatDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICombatStateRepository _combatRepo;

    public GetActiveCombatQueryHandler(
        ICurrentUserService currentUser,
        ICombatStateRepository combatRepo)
    {
        _currentUser = currentUser;
        _combatRepo = combatRepo;
    }

    public async Task<Result<ActiveCombatDto>> Handle(GetActiveCombatQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result.Failure<ActiveCombatDto>(new Error(
                "Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));
        }

        var activeCombatId = await _combatRepo.GetPlayerActiveCombatAsync(userId);

        if (string.IsNullOrEmpty(activeCombatId))
        {
            return new ActiveCombatDto { CombatId = null, MatchType = null };
        }

        // Phantom Data Protection: Ensure the combat actually still exists in Redis.
        // If the Redis TTL expired, the pointer might still exist but the game is gone.
        var gameState = await _combatRepo.GetAsync(activeCombatId);
        if (gameState == null)
        {
            await _combatRepo.ClearPlayerActiveCombatAsync(userId);
            return new ActiveCombatDto { CombatId = null, MatchType = null };
        }

        // Return the Combat ID and the Match Type (PvE vs PvP) so the client knows how to route.
        return new ActiveCombatDto
        {
            CombatId = activeCombatId,
            MatchType = gameState.MatchType
        };
    }
}