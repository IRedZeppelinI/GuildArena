using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.AI.BackgroundServices; // <--- NOVO
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.EndTurn;

public class EndTurnCommandHandlerTests
{
    private readonly ITurnManagerService _turnManagerMock;
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly ICurrentUserService _currentUserMock;
    private readonly ICombatNotifier _notifierMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly IAiTurnQueue _aiQueueMock; 
    private readonly ILogger<EndTurnCommandHandler> _loggerMock;
    private readonly EndTurnCommandHandler _handler;

    public EndTurnCommandHandlerTests()
    {
        _turnManagerMock = Substitute.For<ITurnManagerService>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _notifierMock = Substitute.For<ICombatNotifier>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _aiQueueMock = Substitute.For<IAiTurnQueue>(); 
        _loggerMock = Substitute.For<ILogger<EndTurnCommandHandler>>();

        _handler = new EndTurnCommandHandler(
            _turnManagerMock,
            _combatRepoMock,
            _currentUserMock,
            _notifierMock,
            _battleLogMock,
            _aiQueueMock, 
            _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldAdvanceTurn_WhenUserIsOwnerOfTurn()
    {
        // ARRANGE
        var combatId = Guid.NewGuid().ToString();
        var userId = 100;

        var command = new EndTurnCommand { CombatId = combatId };

        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = userId, // It matches the logged user            
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = userId, Type = CombatPlayerType.Human }
            }
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        _turnManagerMock.Received(1).AdvanceTurn(gameState);
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);

        // Verifica se enviou os updates via SignalR
        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);

        // Garante que NÃO meteu nada na fila da IA, porque o próximo jogador é Humano (ou não é IA)
        await _aiQueueMock.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    // NOVO TESTE: Verifica se enfileira corretamente quando passa o turno para a IA
    [Fact]
    public async Task Handle_ShouldEnqueueAiTurn_WhenNextPlayerIsAi()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 100;
        var aiPlayerId = 999;

        var command = new EndTurnCommand { CombatId = combatId };

        // O Jogador 100 passa o turno. Assumimos que o TurnManager muda o CurrentPlayerId para 999 (IA)
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = userId,
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = userId, Type = CombatPlayerType.Human },
                new CombatPlayer { PlayerId = aiPlayerId, Type = CombatPlayerType.AI }
            }
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // Simulamos o comportamento do TurnManager de mudar de jogador
        _turnManagerMock.When(x => x.AdvanceTurn(gameState)).Do(x => gameState.CurrentPlayerId = aiPlayerId);

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Validamos se o pedido foi enviado para a fila da IA com os IDs corretos!
        await _aiQueueMock.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(req => req.CombatId == combatId && req.AiPlayerId == aiPlayerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns((int?)null); // No User

        var command = new EndTurnCommand { CombatId = "ANY" };

        // ACT & ASSERT
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }
    [Fact]
    public async Task Handle_ShouldThrowKeyNotFound_WhenCombatDoesNotExist()
    {
        // ARRANGE
        var combatId = "INVALID_ID";
        _currentUserMock.UserId.Returns(1); // User exists
        _combatRepoMock.GetAsync(combatId).Returns((GameState?)null); // Combat missing

        var command = new EndTurnCommand { CombatId = combatId };

        // ACT & ASSERT
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenItIsNotUserTurn()
    {
        // ARRANGE
        var combatId = Guid.NewGuid().ToString();
        var userId = 100;
        var enemyId = 200;

        var command = new EndTurnCommand { CombatId = combatId };
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = enemyId // It is the ENEMY's turn
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT & ASSERT
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldBe("It is not your turn.");

        // Ensure we did NOT advance turn or save
        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }
}