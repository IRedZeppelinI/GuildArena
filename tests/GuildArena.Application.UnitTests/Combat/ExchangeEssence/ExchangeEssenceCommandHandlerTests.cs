using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.ExchangeEssence;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Resources;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.ExchangeEssence;

public class ExchangeEssenceCommandHandlerTests
{
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly ICurrentUserService _userMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ICombatNotifier _notifierMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly ILogger<ExchangeEssenceCommandHandler> _loggerMock;
    private readonly ExchangeEssenceCommandHandler _handler;

    public ExchangeEssenceCommandHandlerTests()
    {
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _userMock = Substitute.For<ICurrentUserService>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _notifierMock = Substitute.For<ICombatNotifier>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _loggerMock = Substitute.For<ILogger<ExchangeEssenceCommandHandler>>();

        _handler = new ExchangeEssenceCommandHandler(
            _combatRepoMock,
            _userMock,
            _essenceServiceMock,
            _notifierMock,
            _battleLogMock,
            _loggerMock
        );
    }

    [Fact]
    public async Task Handle_ShouldExecuteExchange_WhenRequestIsValid()
    {
        var combatId = "C1";
        var requestUserId = "user-123";
        var seatId = 1;

        _userMock.UserId.Returns(requestUserId);

        var player = new CombatPlayer
        {
            PlayerId = seatId,
            UserId = requestUserId,
            Type = CombatPlayerType.Human
        };

        var gameState = new GameState
        {
            CurrentPlayerId = seatId,
            Players = new List<CombatPlayer> { player }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(true);

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int>
            {
                { EssenceType.Vigor, 1 },
                { EssenceType.Mind, 1 }
            },
            EssenceToGain = EssenceType.Flux
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        _essenceServiceMock.Received(1).ConsumeEssence(player, command.EssenceToSpend);
        _essenceServiceMock.Received(1).AddEssence(player, command.EssenceToGain, 1);

        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);
    }
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenTotalSpentIsNotExactlyTwo()
    {
        var combatId = "C1";
        var requestUserId = "user-123";
        var seatId = 1;

        _userMock.UserId.Returns(requestUserId);

        var gameState = new GameState
        {
            CurrentPlayerId = seatId,
            Players = new List<CombatPlayer> { new CombatPlayer { PlayerId = seatId, UserId = requestUserId } }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int>
            {
                { EssenceType.Vigor, 3 }
            },
            EssenceToGain = EssenceType.Flux
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Exchange.InvalidAmount");

        _essenceServiceMock.DidNotReceiveWithAnyArgs().ConsumeEssence(default!, default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WhenPlayerHasInsufficientFunds()
    {
        var combatId = "C1";
        var requestUserId = "user-123";
        var seatId = 1;

        _userMock.UserId.Returns(requestUserId);

        var player = new CombatPlayer { PlayerId = seatId, UserId = requestUserId };
        var gameState = new GameState
        {
            CurrentPlayerId = seatId,
            Players = new List<CombatPlayer> { player }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(false);

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int> { { EssenceType.Light, 2 } },
            EssenceToGain = EssenceType.Shadow
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Exchange.InsufficientEssence");

        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotParticipant()
    {
        var combatId = "C1";
        var requestUserId = "user-intruder";

        _userMock.UserId.Returns(requestUserId);

        _combatRepoMock.GetAsync(combatId).Returns(new GameState
        {
            CurrentPlayerId = 1,
            Players = new List<CombatPlayer> { new CombatPlayer { PlayerId = 1, UserId = "user-legit" } }
        });

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 2 } },
            EssenceToGain = EssenceType.Flux
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotParticipant");
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotPlayerTurn()
    {
        var combatId = "C1";
        var requestUserId = "user-123";
        var mySeatId = 1;
        var enemySeatId = 2;

        _userMock.UserId.Returns(requestUserId);

        _combatRepoMock.GetAsync(combatId).Returns(new GameState
        {
            CurrentPlayerId = enemySeatId,
            Players = new List<CombatPlayer> { new CombatPlayer { PlayerId = mySeatId, UserId = requestUserId } }
        });

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 2 } },
            EssenceToGain = EssenceType.Flux
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        result.Error.Code.ShouldBe("Combat.NotYourTurn");

        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserNotAuthenticated()
    {
        _userMock.UserId.Returns((string?)null);

        var command = new ExchangeEssenceCommand { CombatId = "C1" };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Auth.Unauthorized");
    }
}