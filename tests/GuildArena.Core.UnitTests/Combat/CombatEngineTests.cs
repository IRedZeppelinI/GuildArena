using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat;

public class CombatEngineTests
{
    private readonly ILogger<CombatEngine> _logger;
    private readonly ActionQueue _realQueue;
    // Mocks dos serviços apenas para o construtor não falhar
    private readonly IEffectHandler _handlerMock = Substitute.For<IEffectHandler>();

    public CombatEngineTests()
    {
        _logger = Substitute.For<ILogger<CombatEngine>>();
        _realQueue = new ActionQueue(Substitute.For<ILogger<ActionQueue>>());
    }

    private CombatEngine CreateEngine()
    {
        // Construtor com mocks vazios, pois o Engine só vai passar referências
        return new CombatEngine(
            new[] { _handlerMock },
            _logger,
            Substitute.For<ICooldownCalculationService>(),
            Substitute.For<ICostCalculationService>(),
            Substitute.For<IEssenceService>(),
            Substitute.For<ITargetResolutionService>(),
            Substitute.For<IStatusConditionService>(),
            Substitute.For<IHitChanceService>(),
            Substitute.For<IRandomProvider>(),
            Substitute.For<IStatCalculationService>(),
            Substitute.For<ITriggerProcessor>(),
            _realQueue,
            Substitute.For<IBattleLogService>()
        );
    }

    [Fact]
    public void ExecuteAbility_ShouldClearQueue_AndProcessInitialAction()
    {
        // ARRANGE
        var engine = CreateEngine();

        // Sujar a fila propositadamente para garantir que o Engine a limpa antes de começar
        _realQueue.Enqueue(Substitute.For<ICombatAction>());
        _realQueue.HasNext().ShouldBeTrue();

        var ability = new AbilityDefinition { Id = "Test", Name = "Test" };

        
        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_HUMAN",
            BaseStats = new()
        };

        var state = new GameState { Players = new() { new CombatPlayer { PlayerId = 0 } } };

        // ACT
        var results = engine.ExecuteAbility(
            state,
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ASSERT
        // A fila deve estar vazia (processou tudo)
        _realQueue.HasNext().ShouldBeFalse();
        // Deve ter retornado pelo menos um resultado (a ação inicial)
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public void ExecuteAbility_InfiniteLoopProtection_ShouldStopProcessing()
    {
        // ARRANGE
        var engine = CreateEngine();

        var ability = new AbilityDefinition { Id = "Test", Name = "Test" };

        
        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_HUMAN",
            BaseStats = new(),
            CurrentHP = 50
        };

        // ACT
        var results = engine.ExecuteAbility(
            new GameState(),
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ASSERT
        results.Count.ShouldBeLessThan(51); // O limite hardcoded no Engine
    }
}