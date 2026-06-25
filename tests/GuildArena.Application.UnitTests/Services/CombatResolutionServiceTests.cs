using GuildArena.Application.Abstractions;
using GuildArena.Application.Services;
using GuildArena.Domain.Enums.Combat;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Services;

public class CombatResolutionServiceTests
{
    private readonly IMatchTypeResolver _resolverMock;
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly Microsoft.Extensions.Logging.ILogger<CombatResolutionService> _loggerMock;
    private readonly CombatResolutionService _service;

    public CombatResolutionServiceTests()
    {
        _resolverMock = Substitute.For<IMatchTypeResolver>();
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _loggerMock = Substitute.For<Microsoft.Extensions.Logging.ILogger<CombatResolutionService>>();

        _service = new CombatResolutionService(new[] { _resolverMock }, _combatRepoMock, _loggerMock);
    }

    [Fact]
    public async Task ResolveCombatAsync_NoResolverFound_ThrowsException()
    {
        _resolverMock.CanHandle(Arg.Any<MatchType>()).Returns(false);
        var state = new GameState { MatchType = MatchType.PvP };

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.ResolveCombatAsync("combat-1", state, "user-1", false, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResolveCombatAsync_Success_CleansRedisAndPointers()
    {
        // Arrange
        _resolverMock.CanHandle(MatchType.Encounter).Returns(true);
        var expectedResult = new CombatResultDto { IsWinner = true };
        _resolverMock.ResolveMatchAsync("c-1", Arg.Any<GameState>(), "user-1", false, Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var state = new GameState
        {
            MatchType = MatchType.Encounter,
            Players = new List<CombatPlayer>
            {
                new CombatPlayer { Type = CombatPlayerType.Human, UserId = "user-1" },
                new CombatPlayer { Type = CombatPlayerType.AI, UserId = null } // AI shouldn't trigger pointer clear
            }
        };

        // Act
        var result = await _service.ResolveCombatAsync("c-1", state, "user-1", false, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);

        // Verifica que apagou o combate
        await _combatRepoMock.Received(1).DeleteAsync("c-1");

        // Verifica que limpou o ponteiro APENAS para o Human player
        await _combatRepoMock.Received(1).ClearPlayerActiveCombatAsync("user-1");
        await _combatRepoMock.DidNotReceive().ClearPlayerActiveCombatAsync(Arg.Is<string>(s => s == null || s == ""));
    }
}