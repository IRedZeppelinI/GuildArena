using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Handles the EndTurnCommand by orchestrating the core logic
/// and infrastructure services.
/// </summary>
public class EndTurnCommandHandler : IRequestHandler<EndTurnCommand>
{
    private readonly ITurnManagerService _turnManagerService; 
    private readonly ICombatStateRepository _combatStateRepo; 
    private readonly ILogger<EndTurnCommandHandler> _logger;
    //TODO:  Injetar IHubContext<CombatHub> para o SignalR

    public EndTurnCommandHandler(
        ITurnManagerService turnManagerService,
        ICombatStateRepository combatStateRepo,
        ILogger<EndTurnCommandHandler> logger)
    {
        _turnManagerService = turnManagerService;
        _combatStateRepo = combatStateRepo;
        _logger = logger;
    }

    public async Task Handle(EndTurnCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling EndTurnCommand for CombatId: {CombatId}", request.CombatId);

        // 1. CARREGAR (Load)
        var gameState = await _combatStateRepo.GetAsync(request.CombatId);

        if (gameState == null)
        {
            _logger.LogError("Combat state {CombatId} not found.", request.CombatId);
            //TODO: Lançar uma exceção NotFound personalizada)
            return;
        }

        // TODO: Validação - O jogador que fez o pedido é o gameState.CurrentPlayerId?)

        // 2. MODIFICAR (Modify)        
        _turnManagerService.AdvanceTurn(gameState);

        // 3. GUARDAR (Save)        
        await _combatStateRepo.SaveAsync(request.CombatId, gameState);

        // 4. NOTIFICAR (Notify)
        // TODO: Chamar o Hub do SignalR

        _logger.LogInformation("Successfully handled EndTurnCommand for CombatId: {CombatId}", request.CombatId);
    }
}