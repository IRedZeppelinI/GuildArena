using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Combat.StartCombat;

public class StartCombatCommandHandlerTests
{
    private readonly ICombatStateRepository _combatRepoMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ICombatantFactory _factoryMock;
    private readonly ILogger<StartCombatCommandHandler> _loggerMock;
    private readonly IRandomProvider _randomMock;
    private readonly StartCombatCommandHandler _handler;

    public StartCombatCommandHandlerTests()
    {
        _combatRepoMock = Substitute.For<ICombatStateRepository>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _factoryMock = Substitute.For<ICombatantFactory>();
        _loggerMock = Substitute.For<ILogger<StartCombatCommandHandler>>();
        _randomMock = Substitute.For<IRandomProvider>();

        _handler = new StartCombatCommandHandler(
            _combatRepoMock,
            _essenceServiceMock,
            _factoryMock,
            _loggerMock,
            _randomMock
        );
    }

    [Fact]
    public async Task Handle_ShouldInitializeCombat_WithCorrectParticipants()
    {
        // ARRANGE
        // Configurar o Random para escolher sempre o índice 0 (Player 1 começa)
        _randomMock.Next(Arg.Any<int>()).Returns(0);

        // Setup do Mock da Factory para devolver combatentes válidos quando chamada
        _factoryMock.Create(Arg.Any<HeroCharacter>(), Arg.Any<int>(), Arg.Any<List<string>>())
            .Returns(callInfo =>
            {
                var ownerId = callInfo.Arg<int>();
                return new Combatant
                {
                    Id = 100 + ownerId,
                    OwnerId = ownerId,
                    Name = "MockHero",
                    RaceId = "RACE_HUMAN", 
                    BaseStats = new(),
                    MaxHP = 100,
                    CurrentHP = 100
                };
            });

        // O Comando vem já preenchido (simulando o Controller)
        var command = new StartCombatCommand
        {
            Participants = new List<StartCombatCommand.Participant>
            {
                new()
                {
                    PlayerId = 1,
                    Type = CombatPlayerType.Human,
                    Team = new List<StartCombatCommand.HeroSetup>
                    {
                        new() { CharacterDefinitionId = "HERO_A" }
                    }
                },
                new()
                {
                    PlayerId = 2,
                    Type = CombatPlayerType.Human, // PvP Scenario
                    Team = new List<StartCombatCommand.HeroSetup>
                    {
                        new() { CharacterDefinitionId = "HERO_B" }
                    }
                }
            }
        };

        // ACT
        var resultCombatId = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // 1. Deve devolver um ID válido
        Guid.TryParse(resultCombatId, out _).ShouldBeTrue();

        // 2. Deve ter guardado o estado no repositório
        await _combatRepoMock.Received(1).SaveAsync(
            resultCombatId,
            Arg.Is<GameState>(g =>
                g.Players.Count == 2 &&
                g.Combatants.Count == 2 &&
                g.CurrentTurnNumber == 1
            ));

        // 3. Deve ter gerado Essence para o Starting Player (Player 1, index 0)
        // E deve ter passado o baseAmount: 2 (Handicap de início)
        _essenceServiceMock.Received(1).GenerateStartOfTurnEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 1),
            baseAmount: 2
        );
    }

    [Fact]
    public async Task Handle_ShouldSetStartingPlayer_BasedOnRandom()
    {
        // ARRANGE
        // Agora o Random devolve 1 (significa que o segundo jogador da lista começa)
        _randomMock.Next(Arg.Any<int>()).Returns(1);

        var command = new StartCombatCommand
        {
            Participants = new List<StartCombatCommand.Participant>
            {
                new() { PlayerId = 10, Team = new() { new() { CharacterDefinitionId = "A" } } },
                new() { PlayerId = 20, Team = new() { new() { CharacterDefinitionId = "B" } } }
            }
        };

        // ACT
        await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Verificar no SaveAsync se o CurrentPlayerId ficou correto
        await _combatRepoMock.Received(1).SaveAsync(
            Arg.Any<string>(),
            Arg.Is<GameState>(g => g.CurrentPlayerId == 20) // Player 20 deve começar
        );
    }
}