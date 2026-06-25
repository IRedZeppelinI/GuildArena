using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.Reconnection;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.Results;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Combat.Reconnection;

public class GetActiveCombatQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserMock;
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly GetActiveCombatQueryHandler _handler;

    public GetActiveCombatQueryHandlerTests()
    {
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _handler = new GetActiveCombatQueryHandler(_currentUserMock, _combatRepoMock);
    }

    [Fact]
    public async Task Handle_Unauthorized_ReturnsFailure()
    {
        _currentUserMock.UserId.Returns((string?)null);

        var result = await _handler.Handle(new GetActiveCombatQuery(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NoActivePointer_ReturnsNullCombatId()
    {
        _currentUserMock.UserId.Returns("user-1");
        _combatRepoMock.GetPlayerActiveCombatAsync("user-1").Returns((string?)null);

        var result = await _handler.Handle(new GetActiveCombatQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldBeNull();
        result.Value.HasActiveCombat.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_PhantomPointer_CleansUpAndReturnsNull()
    {
        _currentUserMock.UserId.Returns("user-1");
        _combatRepoMock.GetPlayerActiveCombatAsync("user-1").Returns("combat-999");

        // Simula que o combate expirou no Redis (Retorna null)
        _combatRepoMock.GetAsync("combat-999").Returns((GameState?)null);

        var result = await _handler.Handle(new GetActiveCombatQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldBeNull();

        // Verifica se limpou o lixo no Redis
        await _combatRepoMock.Received(1).ClearPlayerActiveCombatAsync("user-1");
    }

    [Fact]
    public async Task Handle_ActiveCombatExists_ReturnsDtoWithMatchType()
    {
        _currentUserMock.UserId.Returns("user-1");
        _combatRepoMock.GetPlayerActiveCombatAsync("user-1").Returns("combat-123");

        var gameState = new GameState { MatchType = MatchType.Dungeon };
        _combatRepoMock.GetAsync("combat-123").Returns(gameState);

        var result = await _handler.Handle(new GetActiveCombatQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CombatId.ShouldBe("combat-123");
        result.Value.MatchType.ShouldBe(MatchType.Dungeon);
        result.Value.HasActiveCombat.ShouldBeTrue();
    }
}