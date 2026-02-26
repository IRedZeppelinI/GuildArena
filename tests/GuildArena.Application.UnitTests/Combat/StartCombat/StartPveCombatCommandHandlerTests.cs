using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Combat.StartCombat;

public class StartPveCombatCommandHandlerTests
{
    // Mocks
    private readonly ICombatStateRepository _combatStateRepoMock;
    private readonly IPlayerRepository _playerRepoMock;
    private readonly IEncounterDefinitionRepository _encounterRepoMock;
    private readonly ICurrentUserService _currentUserMock;
    private readonly ICombatantFactory _factoryMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly ILogger<StartPveCombatCommandHandler> _loggerMock;
    private readonly IRandomProvider _rngMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly ICombatEngine _combatEngineMock;

    // SUT
    private readonly StartPveCombatCommandHandler _handler;

    public StartPveCombatCommandHandlerTests()
    {
        _combatStateRepoMock = Substitute.For<ICombatStateRepository>();
        _playerRepoMock = Substitute.For<IPlayerRepository>();
        _encounterRepoMock = Substitute.For<IEncounterDefinitionRepository>();
        _currentUserMock = Substitute.For<ICurrentUserService>();
        _factoryMock = Substitute.For<ICombatantFactory>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _loggerMock = Substitute.For<ILogger<StartPveCombatCommandHandler>>();
        _rngMock = Substitute.For<IRandomProvider>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _combatEngineMock = Substitute.For<ICombatEngine>();

        _handler = new StartPveCombatCommandHandler(
            _combatStateRepoMock,
            _playerRepoMock,
            _encounterRepoMock,
            _currentUserMock,
            _factoryMock,
            _essenceServiceMock,
            _loggerMock,
            _rngMock,
            _triggerProcessorMock, 
            _combatEngineMock      
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateCombat_WhenRequestIsValid()
    {
        // ARRANGE
        var playerId = 123;
        var encounterId = "ENC_TEST";
        var heroIds = new List<int> { 10, 11 };

        // 1. Mock Auth
        _currentUserMock.UserId.Returns(playerId);

        // 2. Mock Player Heroes (Retorna a quantidade correta para passar a validação)
        var heroesFromDb = new List<Hero>
        {
            new() { Id = 10, CharacterDefinitionId = "HERO_A" },
            new() { Id = 11, CharacterDefinitionId = "HERO_B" }
        };
        _playerRepoMock.GetHeroesAsync(playerId, heroIds).Returns(heroesFromDb);

        // 3. Mock Encounter Definition
        var encounterDef = new EncounterDefinition
        {
            Id = encounterId,
            Name = "Test Encounter",
            Enemies = new List<EncounterDefinition.EncounterEnemy>
            {
                // Definimos uma posição específica (ex: 5) para verificar se é mapeada
                new() { CharacterDefinitionId = "MOB_A", Position = 5 }
            }
        };
        _encounterRepoMock.TryGetDefinition(encounterId, out Arg.Any<EncounterDefinition>())
            .Returns(x =>
            {
                x[1] = encounterDef;
                return true;
            });

        // 4. Mock Factory (Devolve um combatente genérico para não rebentar)
        _factoryMock.Create(Arg.Any<Hero>(), Arg.Any<int>())
            .Returns(info => new Combatant
            {
                // Usar o ID do HeroCharacter passado para garantir unicidade no teste
                Id = info.Arg<Hero>().Id,
                Name = "Mock",
                RaceId = "RACE_MOCK",
                OwnerId = info.Arg<int>(), // Usa o ownerId passado no Create
                BaseStats = new(),
                CurrentHP = 10
            });

        // 5. Mock RNG (Para determinar quem começa - 0 = Player)
        _rngMock.Next(2).Returns(0);

        var command = new StartPveCombatCommand
        {
            EncounterId = encounterId,
            HeroInstanceIds = heroIds
        };

        // ACT
        var result = await _handler.Handle(command, CancellationToken.None);

        // ASSERT
        // Deve retornar um GUID válido
        Guid.TryParse(result, out _).ShouldBeTrue();

        // Deve ter guardado no Redis
        await _combatStateRepoMock.Received(1).SaveAsync(result, Arg.Any<GameState>());

        _triggerProcessorMock.Received()
            .ProcessTriggers(ModifierTrigger.ON_COMBAT_START, Arg.Any<TriggerContext>());
        _combatEngineMock.Received(1).ProcessPendingActions(Arg.Any<GameState>());


        // Verificação detalhada do GameState guardado
        await _combatStateRepoMock.Received(1).SaveAsync(
            Arg.Any<string>(),
            Arg.Is<GameState>(gs =>
                gs.Players.Count == 2 &&
                gs.Combatants.Count == 3 &&
                gs.CurrentPlayerId == playerId &&

                // --- NOVAS VERIFICAÇÕES DE POSIÇÃO ---
                // O Player pediu 2 heróis. Como decidimos 0-based:
                // Heroi 1 (ID 10) -> Position 0
                gs.Combatants.Any(c => c.Id == 10 && c.OwnerId == playerId && c.Position == 0) &&

                // Heroi 2 (ID 11) -> Position 1
                gs.Combatants.Any(c => c.Id == 11 && c.OwnerId == playerId && c.Position == 1) &&

                // Mob (ID < 0) -> Position 5 (conforme definido no encounterDef acima)
                gs.Combatants.Any(c => c.OwnerId == 0 && c.Position == 5)
            )
        );
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenUserNotAuthenticated()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns((int?)null);

        var command = new StartPveCombatCommand { EncounterId = "ANY", HeroInstanceIds = new() { 1 } };

        // ACT & ASSERT
        await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowArgumentException_WhenNoHeroesSelected()
    {
        // ARRANGE
        _currentUserMock.UserId.Returns(1);
        var command = new StartPveCombatCommand
        {
            EncounterId = "ENC",
            HeroInstanceIds = new List<int>() // Lista vazia
        };

        // ACT & ASSERT
        await Should.ThrowAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldThrowInvalidOperation_WhenPlayerDoesNotOwnHero()
    {
        // ARRANGE
        var playerId = 1;
        var requestedIds = new List<int> { 10, 999 }; // 999 não existe

        _currentUserMock.UserId.Returns(playerId);

        // O repo só devolve o ID 10
        _playerRepoMock.GetHeroesAsync(playerId, requestedIds)
            .Returns(new List<Hero> { new() { Id = 10, CharacterDefinitionId = "H" } });

        var command = new StartPveCombatCommand
        {
            EncounterId = "ENC",
            HeroInstanceIds = requestedIds
        };

        // ACT & ASSERT
        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        ex.Message.ShouldContain("do not exist or do not belong");
    }

    [Fact]
    public async Task Handle_ShouldThrowKeyNotFound_WhenEncounterDoesNotExist()
    {
        // ARRANGE
        var playerId = 1;
        var encounterId = "INVALID_ENC";

        _currentUserMock.UserId.Returns(playerId);

        // Repo devolve os heróis corretamente
        _playerRepoMock.GetHeroesAsync(playerId, Arg.Any<List<int>>())
            .Returns(new List<Hero> { new() { Id = 1, CharacterDefinitionId = "H" } });

        // Encounter Repo falha
        _encounterRepoMock.TryGetDefinition(encounterId, out Arg.Any<EncounterDefinition>())
            .Returns(false);

        var command = new StartPveCombatCommand
        {
            EncounterId = encounterId,
            HeroInstanceIds = new List<int> { 1 }
        };

        // ACT & ASSERT
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}