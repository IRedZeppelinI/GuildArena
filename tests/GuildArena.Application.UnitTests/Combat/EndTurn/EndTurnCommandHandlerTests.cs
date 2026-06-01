using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Combat;
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
    private readonly ICombatResolutionService _resolutionServiceMock;
    private readonly EndTurnCommandHandler _handler;

    public EndTurnCommandHandlerTests()
    {
        _turnManagerMock = Substitute.For<ITurnManagerService>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _notifierMock = Substitute.For<ICombatNotifier>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _aiQueueMock = Substitute.For<IAiTurnQueue>();
        _resolutionServiceMock = Substitute.For<ICombatResolutionService>();
        _loggerMock = Substitute.For<ILogger<EndTurnCommandHandler>>();

        _handler = new EndTurnCommandHandler(
            _turnManagerMock,
            _combatRepoMock,
            _currentUserMock,
            _notifierMock,
            _battleLogMock,
            _aiQueueMock,
            _resolutionServiceMock,
            _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldAdvanceTurn_WhenUserIsOwnerOfTurn()
    {
        // ARRANGE
        var combatId = "C1";
        var requestUserId = "user-123"; // O GUID do utilizador
        var seatId = 1;                 // A cadeira que lhe pertence

        var command = new EndTurnCommand { CombatId = combatId };

        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = seatId, // É o turno da cadeira 1
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = seatId, UserId = requestUserId, Type = CombatPlayerType.Human }
            }
        };

        _currentUserMock.UserId.Returns(requestUserId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        _turnManagerMock.Received(1).AdvanceTurn(gameState);
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);

        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);

        // O próximo não é IA neste setup, por isso não envia para a queue
        await _aiQueueMock.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldEnqueueAiTurn_WhenNextPlayerIsAi()
    {
        // ARRANGE
        var combatId = "C1";
        var requestUserId = "user-123";
        var mySeatId = 1;
        var aiSeatId = -1;

        var command = new EndTurnCommand { CombatId = combatId };

        var gameState = new GameState
        {
            CurrentTurnNumber = 1,
            CurrentPlayerId = mySeatId,
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = mySeatId, UserId = requestUserId, Type = CombatPlayerType.Human },
                new CombatPlayer { PlayerId = aiSeatId, UserId = null, Type = CombatPlayerType.AI }
            }
        };

        _currentUserMock.UserId.Returns(requestUserId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // Simular o TurnManager a passar a vez para a Cadeira da IA (-1)
        _turnManagerMock.When(x => x.AdvanceTurn(gameState))
            .Do(x => gameState.CurrentPlayerId = aiSeatId);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        await _aiQueueMock.Received(1).EnqueueAsync(
            Arg.Is<AiTurnRequest>(req => req.CombatId == combatId && req.AiPlayerId == aiSeatId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsNotAuthenticated()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns((string?)null);

        var command = new EndTurnCommand { CombatId = "ANY" };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserIsNotParticipant()
    {
        // ARRANGE
        var combatId = "C1";
        var requestUserId = "user-intruder";

        var command = new EndTurnCommand { CombatId = combatId };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = 1, UserId = "user-legit" }
            }
        };

        _currentUserMock.UserId.Returns(requestUserId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotParticipant");
    }
    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenItIsNotUserTurn()
    {
        // ARRANGE
        var combatId = "C1";
        var requestUserId = "user-123";
        var mySeatId = 1;
        var enemySeatId = 2;

        var command = new EndTurnCommand { CombatId = combatId };
        var gameState = new GameState
        {
            CurrentPlayerId = enemySeatId, // O turno é da cadeira 2
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { PlayerId = mySeatId, UserId = requestUserId }
            }
        };

        _currentUserMock.UserId.Returns(requestUserId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotYourTurn");
    }


    [Fact]
    public async Task Handle_ShouldResolveAndNotify_WhenCombatEndsAfterTurn()
    {
        // ARRANGE
        var combatId = "C-END";
        var userId = "user-1";
        var seatId = 1;

        _currentUserMock.UserId.Returns(userId);

        var player = new CombatPlayer { PlayerId = seatId, UserId = userId, Type = CombatPlayerType.Human };
        var ai = new CombatPlayer { PlayerId = -1, UserId = null, Type = CombatPlayerType.AI };

        var gameState = new GameState
        {
            CurrentPlayerId = seatId,
            Players = new List<CombatPlayer> { player, ai },
            Status = CombatStatus.Player1Won   // combate já terminou (ex.: último inimigo morreu por DoT no início do turno)
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);
        _battleLogMock.GetAndClearLogs().Returns(new List<string>());

        var expectedDto = new CombatResultDto
        {
            IsWinner = true,
            XpGained = 50,
            GoldGained = 100,
            NewGuildLevel = 2,
            IsSurrender = false
        };
        _resolutionServiceMock
            .ResolveCombatAsync(combatId, gameState, userId, false, Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var command = new EndTurnCommand { CombatId = combatId };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        // O turno ainda avança (comportamento atual do handler)
        _turnManagerMock.Received(1).AdvanceTurn(gameState);

        // A resolução deve ser chamada uma vez
        await _resolutionServiceMock.Received(1).ResolveCombatAsync(combatId, gameState, userId, false, Arg.Any<CancellationToken>());

        // A notificação de fim de combate deve ser enviada
        await _notifierMock.Received(1).SendCombatEndedAsync(combatId, expectedDto);
    }
}