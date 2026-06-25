using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.Reconnection;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.Reconnection;

public class GetReconnectedCombatStateQueryHandlerTests
{
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly GetReconnectedCombatStateQueryHandler _handler;

    public GetReconnectedCombatStateQueryHandlerTests()
    {
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _handler = new GetReconnectedCombatStateQueryHandler(_combatRepoMock);
    }

    [Fact]
    public async Task Handle_CombatNotFound_ReturnsNotFoundFailure()
    {
        _combatRepoMock.GetAsync("combat-1").Returns((GameState?)null);
        var query = new GetReconnectedCombatStateQuery { CombatId = "combat-1" };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_CombatExists_ReturnsDomainState()
    {
        var gameState = new GameState { CurrentTurnNumber = 5 };
        _combatRepoMock.GetAsync("combat-1").Returns(gameState);
        var query = new GetReconnectedCombatStateQuery { CombatId = "combat-1" };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldBe("combat-1");
        result.Value.InitialState.ShouldBe(gameState);
        result.Value.InitialLogs.ShouldContain("--- Reconnected to active combat ---");
    }
}