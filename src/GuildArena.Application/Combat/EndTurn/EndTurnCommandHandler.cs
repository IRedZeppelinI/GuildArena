using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Handles the request to end the current turn.
/// Validates ownership of the turn and orchestrates the transition to the next player.
/// </summary>
public class EndTurnCommandHandler : IRequestHandler<EndTurnCommand>
{
    private readonly ITurnManagerService _turnManagerService;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EndTurnCommandHandler> _logger;

    public EndTurnCommandHandler(
        ITurnManagerService turnManagerService,
        ICombatStateRepository combatStateRepo,
        ICurrentUserService currentUserService,
        ILogger<EndTurnCommandHandler> logger)
    {
        _turnManagerService = turnManagerService;
        _combatStateRepo = combatStateRepo;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Processes the end turn command.
    /// </summary>
    /// <param name="request">The command containing the combat ID.</param>
    /// <param name="cancellationToken">Cancellation token </param>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authenticated.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the combat session does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the user attempts to end a turn that is not theirs.</exception>
    public async Task Handle(EndTurnCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Authentication
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            _logger.LogWarning
                ("Anonymous user attempted to end turn for Combat {CombatId}.", request.CombatId);
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        // 2. Load Game State
        var gameState = await _combatStateRepo.GetAsync(request.CombatId);
        if (gameState == null)
        {
            _logger.LogWarning("Combat session {CombatId} not found.", request.CombatId);
            throw new KeyNotFoundException($"Combat {request.CombatId} not found.");
        }

        // 3. Validate Turn Ownership
        // Only the player whose ID matches CurrentPlayerId can end the turn.
        if (gameState.CurrentPlayerId != userId)
        {
            _logger.LogWarning(
                "User {UserId} tried to end turn for Player {CurrentPlayerId} in Combat {CombatId}.",
                userId, gameState.CurrentPlayerId, request.CombatId);

            throw new InvalidOperationException("It is not your turn.");
        }

        // 4. Execute Domain Logic
        _logger.LogInformation(
            "User {UserId} ending turn {TurnNumber} in Combat {CombatId}.",
            userId, gameState.CurrentTurnNumber, request.CombatId);

        _turnManagerService.AdvanceTurn(gameState);

        // 5. Persist Changes
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        _logger.LogInformation(
            "Combat {CombatId} advanced to Player {NextPlayer}.",
            request.CombatId, gameState.CurrentPlayerId);

        // TODO: Notify clients via SignalR about the turn change.
    }
}