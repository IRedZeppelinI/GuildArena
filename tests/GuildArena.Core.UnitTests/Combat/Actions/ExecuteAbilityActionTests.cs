using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.Targeting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Actions;

public class ExecuteAbilityActionTests
{
    private readonly ICombatEngine _engineMock;

    // Serviços Mockados
    private readonly ICostCalculationService _costMock;
    private readonly IEssenceService _essenceMock;
    private readonly IStatusConditionService _statusMock;
    private readonly IStatCalculationService _statMock;
    private readonly ICooldownCalculationService _cooldownMock;
    private readonly ITargetResolutionService _targetMock;
    private readonly IHitChanceService _hitChanceMock;
    private readonly IEffectHandler _handlerMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly IBattleLogService _battleLogService;
    private readonly IRandomProvider _randomMock;

    public ExecuteAbilityActionTests()
    {
        _engineMock = Substitute.For<ICombatEngine>();

        _costMock = Substitute.For<ICostCalculationService>();
        _essenceMock = Substitute.For<IEssenceService>();
        _statusMock = Substitute.For<IStatusConditionService>();
        _statMock = Substitute.For<IStatCalculationService>();
        _cooldownMock = Substitute.For<ICooldownCalculationService>();
        _targetMock = Substitute.For<ITargetResolutionService>();
        _hitChanceMock = Substitute.For<IHitChanceService>();
        _handlerMock = Substitute.For<IEffectHandler>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _battleLogService = Substitute.For<IBattleLogService>();
        _randomMock = Substitute.For<IRandomProvider>();

        // Ligar mocks ao Engine
        _engineMock.CostService.Returns(_costMock);
        _engineMock.EssenceService.Returns(_essenceMock);
        _engineMock.StatusService.Returns(_statusMock);
        _engineMock.StatService.Returns(_statMock);
        _engineMock.CooldownService.Returns(_cooldownMock);
        _engineMock.TargetService.Returns(_targetMock);
        _engineMock.HitChanceService.Returns(_hitChanceMock);
        _engineMock.TriggerProcessor.Returns(_triggerProcessorMock);
        _engineMock.AppLogger.Returns(Substitute.For<ILogger<ICombatEngine>>());
        _engineMock.GetEffectHandler(Arg.Any<EffectType>()).Returns(_handlerMock);

        _engineMock.Random.Returns(_randomMock); 

        _engineMock.BattleLog.Returns(_battleLogService);

        // Defaults
        _statMock.GetStatValue(Arg.Any<Combatant>(), StatType.MaxActions).Returns(1);
        _statusMock.CheckStatusConditions(Arg.Any<Combatant>(), Arg.Any<AbilityDefinition>())
            .Returns(ActionStatusResult.Allowed);
        _hitChanceMock.CalculateHitChance(Arg.Any<Combatant>(), Arg.Any<Combatant>(), Arg.Any<EffectDefinition>())
            .Returns(1.0f);
        _randomMock.NextDouble().Returns(0.0);
    }

    [Fact]
    public void Execute_WhenResourcesAreSufficient_ShouldSucceed()
    {
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Fireball");
        var player = new CombatPlayer { PlayerId = 1 };
        var state = new GameState { Players = new() { player } };

        var costs = new FinalAbilityCosts { HPCost = 10, EssenceCosts = new() };

        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(costs);
        _essenceMock.HasEnoughEssence(default!, default!).ReturnsForAnyArgs(true);

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        var result = action.Execute(_engineMock, state);

        result.IsSuccess.ShouldBeTrue();
        source.CurrentHP.ShouldBe(90);
        _battleLogService.Received(1).Log(Arg.Is<string>(s => s.Contains("used Fireball")));
    }

    [Fact]
    public void Execute_WhenStunned_ShouldFail()
    {
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Attack");
        _statusMock.CheckStatusConditions(source, ability).Returns(ActionStatusResult.Stunned);

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        var result = action.Execute(_engineMock, new GameState());

        result.IsSuccess.ShouldBeFalse();
        _costMock.DidNotReceive().CalculateFinalCosts(default!, default!, default!, default!);
    }

    [Fact]
    public void Execute_WhenNotEnoughHP_ShouldFail()
    {
        var source = CreateCombatant(1, 1);
        source.CurrentHP = 5;
        var ability = CreateAbility("BloodMagic");
        var player = new CombatPlayer { PlayerId = 1 };
        var costs = new FinalAbilityCosts { HPCost = 10 };

        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(costs);

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        var result = action.Execute(_engineMock, new GameState { Players = new() { player } });

        result.IsSuccess.ShouldBeFalse();
        source.CurrentHP.ShouldBe(5);
    }

    [Fact]
    public void Execute_WhenOutOfActionPoints_ShouldFail()
    {
        var source = CreateCombatant(1, 1);
        source.ActionsTakenThisTurn = 1;
        _statMock.GetStatValue(source, StatType.MaxActions).Returns(1);
        var ability = CreateAbility("BigHit");
        ability.ActionPointCost = 1;

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        var result = action.Execute(_engineMock, new GameState());

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Execute_WhenOnCooldown_ShouldFail()
    {
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Ulti");
        source.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = ability.Id, TurnsRemaining = 2 });

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        var result = action.Execute(_engineMock, new GameState());

        result.IsSuccess.ShouldBeFalse();
    }
    
    [Fact]
    public void Execute_WhenAttackMisses_ShouldLogMissAndSkipEffect()
    {
        // ARRANGE
        // OwnerId definido para evitar crash no CanExecute
        var source = new Combatant { Id = 1, OwnerId = 1, Name = "Source", RaceId = "A", BaseStats = new() { MaxActions = 10 }, MaxHP = 100, CurrentHP = 100 };
        var target = new Combatant { Id = 2, OwnerId = 2, Name = "Target", RaceId = "B", BaseStats = new(), MaxHP = 100, CurrentHP = 100 };
        var state = new GameState
        {
            Combatants = new() { source, target },
            Players = new() { new CombatPlayer { PlayerId = 1 } }
        };

        var effectDef = new EffectDefinition { Type = EffectType.DAMAGE, CanBeEvaded = true, TargetRuleId = "T1" };
        var ability = new AbilityDefinition { Id = "Attack", Name = "Attack", Effects = new() { effectDef }, TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } } };

        _targetMock.ResolveTargets(default!, default!, default!, default!).ReturnsForAnyArgs(new List<Combatant> { target });
        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(default!, default!).ReturnsForAnyArgs(true);

        // Miss
        _hitChanceMock.CalculateHitChance(default!, default!, default!).ReturnsForAnyArgs(0.5f);
        _randomMock.NextDouble().Returns(0.9); // <--- Usar variável de classe

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();
        result.ResultTags.ShouldContain("Miss");
        _handlerMock.DidNotReceiveWithAnyArgs().Apply(default!, default!, default!, default!, default!);
    }

    [Fact]
    public void Execute_WhenPlayerLiesAboutPayment_ShouldFail()
    {
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("CheatSkill");
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool[EssenceType.Vigor] = 5;
        var state = new GameState { Players = new() { player } };

        var costs = new FinalAbilityCosts
        {
            HPCost = 0,
            EssenceCosts = new() { new EssenceAmount { Type = EssenceType.Neutral, Amount = 1 } }
        };

        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(costs);
        _essenceMock.HasEnoughEssence(default!, default!).ReturnsForAnyArgs(true);

        var paymentLie = new Dictionary<EssenceType, int> { { EssenceType.Mind, 1 } };
        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), paymentLie);

        var result = action.Execute(_engineMock, state);

        result.IsSuccess.ShouldBeFalse();
        _engineMock.AppLogger.Received(1).Log(LogLevel.Warning, Arg.Any<EventId>(), Arg.Is<object>(o => o.ToString()!.Contains("possess")), null, Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Execute_WhenTargetInvulnerable_ShouldLogAndSkip()
    {
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);
        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "IceBlock",
            ActiveStatusEffects = new() { StatusEffectType.Invulnerable }
        });

        var state = new GameState
        {
            Combatants = new() { source, target },
            Players = new() { new CombatPlayer { PlayerId = 1 } }
        };

        var effectDef = new EffectDefinition { Type = EffectType.DAMAGE, TargetRuleId = "T1" };
        var ability = new AbilityDefinition { Id = "Nuke", Name = "Nuke", Effects = new() { effectDef }, TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } } };

        _targetMock.ResolveTargets(default!, default!, default!, default!).ReturnsForAnyArgs(new List<Combatant> { target });
        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(default!, default!).ReturnsForAnyArgs(true);

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());
        action.Execute(_engineMock, state);

        _battleLogService.Received(1).Log(Arg.Is<string>(s => s.Contains("is invulnerable")));
        _handlerMock.DidNotReceiveWithAnyArgs().Apply(default!, default!, default!, default!, default!);
    }

    [Fact]
    public void Execute_WhenAttackMisses_ShouldTriggerOnEvade()
    {
        // ARRANGE
        var source = new Combatant { Id = 1, OwnerId = 1, Name = "Attacker", RaceId = "A", BaseStats = new() { MaxActions = 10 }, MaxHP = 100, CurrentHP = 100 };
        var target = new Combatant { Id = 2, OwnerId = 2, Name = "Dodger", RaceId = "B", BaseStats = new(), MaxHP = 100, CurrentHP = 100 };

        var state = new GameState
        {
            Combatants = new() { source, target },
            Players = new() { new CombatPlayer { PlayerId = 1 } }
        };

        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            CanBeEvaded = true,
            TargetRuleId = "T1"
        };

        var ability = new AbilityDefinition
        {
            Id = "Attack",
            Name = "Attack",
            Effects = new() { effectDef },
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } }
        };

        _targetMock.ResolveTargets(default!, default!, default!, default!).ReturnsForAnyArgs(new List<Combatant> { target });
        _costMock.CalculateFinalCosts(default!, default!, default!, default!).ReturnsForAnyArgs(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(default!, default!).ReturnsForAnyArgs(true);

        // MISS
        _hitChanceMock.CalculateHitChance(default!, default!, default!).ReturnsForAnyArgs(0.5f);
        _randomMock.NextDouble().Returns(0.9);

        // Mock Local
        var localTriggerProcessor = Substitute.For<ITriggerProcessor>();
        _engineMock.TriggerProcessor.Returns(localTriggerProcessor);

        var action = new ExecuteAbilityAction(ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        result.ResultTags.ShouldContain("Miss");
        localTriggerProcessor.Received(1).ProcessTriggers(ModifierTrigger.ON_EVADE, Arg.Any<TriggerContext>());
        _handlerMock.DidNotReceiveWithAnyArgs().Apply(default!, default!, default!, default!, default!);
    }

    // --- Helpers ---
    private Combatant CreateCombatant(int id, int ownerId)
    {
        return new Combatant
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"C{id}",
            RaceId = "RACE_DEFAULT",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { MaxActions = 10 } // Garantir AP
        };
    }

    private AbilityDefinition CreateAbility(string id)
    {
        return new AbilityDefinition { Id = id, Name = id };
    }
}