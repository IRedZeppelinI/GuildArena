using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Entities;
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
    private readonly ILogger<EndTurnCommandHandler> _loggerMock;
    private readonly EndTurnCommandHandler _handler;

    public EndTurnCommandHandlerTests()
    {
        _turnManagerMock = Substitute.For<ITurnManagerService>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _loggerMock = Substitute.For<ILogger<EndTurnCommandHandler>>();

        _handler = new EndTurnCommandHandler(
            _turnManagerMock,
            _combatRepoMock,
            _currentUserMock,
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
            CurrentPlayerId = userId // It matches the logged user
        };

        _currentUserMock.UserId.Returns(userId);
        _combatRepoMock.GetAsync(combatId).Returns(gameState);

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        _turnManagerMock.Received(1).AdvanceTurn(gameState);
        await _combatRepoMock.Received(1).SaveAsync(combatId, gameState);
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