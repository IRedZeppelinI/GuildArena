using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

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
            CurrentPlayerId = userId,
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = userId, Type = CombatPlayerType.Human }
            }
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        _turnManagerMock.Received(1).AdvanceTurn(gameState);
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);

        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);

        await _aiQueueMock.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldEnqueueAiTurn_WhenNextPlayerIsAi()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 100;
        var aiPlayerId = 999;

        var command = new EndTurnCommand { CombatId = combatId };

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

        _turnManagerMock.When(x => x.AdvanceTurn(gameState))
            .Do(x => gameState.CurrentPlayerId = aiPlayerId);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        await _aiQueueMock.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(req => req.CombatId == combatId && req.AiPlayerId == aiPlayerId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns((int?)null); // No User

        var command = new EndTurnCommand { CombatId = "ANY" };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Auth.Unauthorized");

        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCombatDoesNotExist()
    {
        // ARRANGE
        var combatId = "INVALID_ID";
        _currentUserMock.UserId.Returns(1);
        _combatRepoMock.GetAsync(combatId).Returns((GameState?)null); // Missing

        var command = new EndTurnCommand { CombatId = combatId };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Combat.NotFound");

        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenItIsNotUserTurn()
    {
        // ARRANGE
        var combatId = Guid.NewGuid().ToString();
        var userId = 100;
        var enemyId = 200;

        var command = new EndTurnCommand { CombatId = combatId };
        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = enemyId // ENEMY's turn
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotYourTurn");

        _turnManagerMock.DidNotReceiveWithAnyArgs().AdvanceTurn(default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }
}