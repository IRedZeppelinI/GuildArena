using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Combat.ExchangeEssence;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
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
        // ARRANGE
        var combatId = "C1";
        var userId = 1;

        _userMock.UserId.Returns(userId);

        var player = new CombatPlayer { PlayerId = userId, Type = CombatPlayerType.Human };
        var gameState = new GameState
        {
            CurrentPlayerId = userId,
            Players = new List<CombatPlayer> { player }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // Mock que aprova os fundos
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

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Verifica se os serviços do motor de combate foram chamados corretamente
        _essenceServiceMock.Received(1).ConsumeEssence(player, command.EssenceToSpend);
        _essenceServiceMock.Received(1).AddEssence(player, command.EssenceToGain, 1);

        // Verifica se guardou e notificou
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
        await _notifierMock.Received(1).SendBattleLogsAsync(combatId, Arg.Any<List<string>>());
        await _notifierMock.Received(1).SendGameStateUpdateAsync(combatId, gameState);
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenTotalSpentIsNotExactlyTwo()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 1;

        _userMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(new GameState { CurrentPlayerId = userId });

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int>
            {
                { EssenceType.Vigor, 3 } // Regra quebrada: Total é 3 em vez de 2
            },
            EssenceToGain = EssenceType.Flux
        };

        // ACT & ASSERT
        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("requires exactly 2 essence");

        // Garante que não fez alterações no sistema
        _essenceServiceMock.DidNotReceiveWithAnyArgs().ConsumeEssence(default!, default!);
        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenPlayerHasInsufficientFunds()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 1;

        _userMock.UserId.Returns(userId);

        var player = new CombatPlayer { PlayerId = userId };
        var gameState = new GameState
        {
            CurrentPlayerId = userId,
            Players = new List<CombatPlayer> { player }
        };

        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // O serviço diz que o jogador NÃO tem as essências que está a tentar trocar
        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(false);

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int> { { EssenceType.Light, 2 } },
            EssenceToGain = EssenceType.Shadow
        };

        // ACT & ASSERT
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenNotPlayerTurn()
    {
        // ARRANGE
        var combatId = "C1";
        var userId = 1;
        var enemyId = 2;

        _userMock.UserId.Returns(userId);

        _combatRepoMock.GetAsync(combatId).Returns(new GameState
        {
            CurrentPlayerId = enemyId // Turno do inimigo
        });

        var command = new ExchangeEssenceCommand
        {
            CombatId = combatId,
            EssenceToSpend = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 2 } },
            EssenceToGain = EssenceType.Flux
        };

        // ACT & ASSERT
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        await _combatRepoMock.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        // ARRANGE
        _userMock.UserId.Returns((int?)null);

        var command = new ExchangeEssenceCommand { CombatId = "C1" };

        // ACT & ASSERT
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}