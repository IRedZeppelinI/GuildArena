using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.Targeting;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;

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
        _engineMock.Random.Returns(Substitute.For<IRandomProvider>());
        _engineMock.BattleLog.Returns(_battleLogService);

        // Defaults para passar nas validações básicas
        _statMock.GetStatValue(Arg.Any<Combatant>(), StatType.MaxActions).Returns(1);
        _statusMock.CheckStatusConditions(
            Arg.Any<Combatant>(),
            Arg.Any<AbilityDefinition>())
            .Returns(ActionStatusResult.Allowed);
        _hitChanceMock.CalculateHitChance(
            Arg.Any<Combatant>(),
            Arg.Any<Combatant>(),
            Arg.Any<EffectDefinition>())
            .Returns(1.0f);
        _engineMock.Random.NextDouble().Returns(0.0); // Garante hit
    }

    [Fact]
    public void Execute_WhenResourcesAreSufficient_ShouldSucceed()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Fireball");
        var player = new CombatPlayer { PlayerId = 1 };
        var state = new GameState { Players = new() { player } };

        var costs = new FinalAbilityCosts { HPCost = 10, EssenceCosts = new() };

        
        _costMock.CalculateFinalCosts(
            player,
            ability,
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>())
            .Returns(costs);

        _essenceMock.HasEnoughEssence(player, costs.EssenceCosts).Returns(true);

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();
        source.CurrentHP.ShouldBe(90);
        _battleLogService.Received(1)
            .Log(Arg.Is<string>(s => s.Contains("used Fireball")));
        
    }

    [Fact]
    public void Execute_WhenStunned_ShouldFail()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Attack");

        _statusMock.CheckStatusConditions(source, ability).Returns(ActionStatusResult.Stunned);

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, new GameState());

        // ASSERT
        result.IsSuccess.ShouldBeFalse();

        
        _costMock.DidNotReceive().CalculateFinalCosts(
            Arg.Any<CombatPlayer>(),
            Arg.Any<AbilityDefinition>(),
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>());
    }

    [Fact]
    public void Execute_WhenNotEnoughHP_ShouldFail()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        source.CurrentHP = 5; // Vida insuficiente

        var ability = CreateAbility("BloodMagic");
        var player = new CombatPlayer { PlayerId = 1 };

        var costs = new FinalAbilityCosts { HPCost = 10 };

        
        _costMock.CalculateFinalCosts(
            player,
            ability,
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>())
            .Returns(costs);

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, new GameState { Players = new() { player } });

        // ASSERT
        result.IsSuccess.ShouldBeFalse();
        source.CurrentHP.ShouldBe(5);
    }

    [Fact]
    public void Execute_WhenOutOfActionPoints_ShouldFail()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        source.ActionsTakenThisTurn = 1; // Já gastou
        _statMock.GetStatValue(source, StatType.MaxActions).Returns(1);

        var ability = CreateAbility("BigHit");
        ability.ActionPointCost = 1;

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, new GameState());

        // ASSERT
        result.IsSuccess.ShouldBeFalse();
        
        _costMock.DidNotReceive().CalculateFinalCosts(
            Arg.Any<CombatPlayer>(),
            Arg.Any<AbilityDefinition>(),
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>());
    }

    [Fact]
    public void Execute_WhenOnCooldown_ShouldFail()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("Ulti");
        source.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = ability.Id, TurnsRemaining = 2 });

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, new GameState());

        // ASSERT
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Execute_WhenAttackMisses_ShouldLogMissAndSkipEffect()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);
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

        var ability = CreateAbility("Attack");
        ability.Effects = new() { effectDef };
        ability.TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } };

        // Mock Resolve Targets
        _targetMock.ResolveTargets(Arg.Any<TargetingRule>(), source, state, Arg.Any<AbilityTargets>())
            .Returns(new List<Combatant> { target });

        // Mocks para passar validações       
        _costMock.CalculateFinalCosts(
            Arg.Any<CombatPlayer>(),
            ability,
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>())
            .Returns(new FinalAbilityCosts());

        _essenceMock.HasEnoughEssence(
            Arg.Any<CombatPlayer>(),
            Arg.Any<List<EssenceAmount>>())
            .Returns(true);

        // CONFIGURAR MISS
        _hitChanceMock.CalculateHitChance(source, target, effectDef).Returns(0.5f);
        _engineMock.Random.NextDouble().Returns(0.6); // Miss

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        result.IsSuccess.ShouldBeTrue(); // A ação executou, apenas falhou o hit
        result.ResultTags.ShouldContain("Miss");
        _handlerMock.DidNotReceive().Apply(
            Arg.Any<EffectDefinition>(),
            Arg.Any<Combatant>(),
            Arg.Any<Combatant>(),
            Arg.Any<GameState>(),
            Arg.Any<CombatActionResult>());
    }

    [Fact]
    public void Execute_WhenPlayerLiesAboutPayment_ShouldFail()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var ability = CreateAbility("CheatSkill");
        var player = new CombatPlayer { PlayerId = 1 };

        // Cenário: O Player tem 5 Vigor. Não tem Mind.
        player.EssencePool[EssenceType.Vigor] = 5;

        var state = new GameState { Players = new() { player } };

        // O custo da habilidade é 1 Neutral (teoricamente pagável com Vigor)
        var costs = new FinalAbilityCosts
        {
            HPCost = 0,
            EssenceCosts = new() { new EssenceAmount { Type = EssenceType.Neutral, Amount = 1 } }
        };

        // Mock 1: O custo é calculado corretamente
        _costMock.CalculateFinalCosts(
            player,
            ability,
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>())
            .Returns(costs);

        // Mock 2: A validação teórica passa (porque 5 Vigor > 1 Neutral)
        _essenceMock.HasEnoughEssence(
            Arg.Any<CombatPlayer>(),
            Arg.Any<List<EssenceAmount>>())
            .Returns(true);

        // A BATOTA: O jogador tenta pagar com 1 Mind (que não tem)
        var paymentLie = new Dictionary<EssenceType, int> { { EssenceType.Mind, 1 } };

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            paymentLie); 

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        // Deve falhar porque a validação de ownership deteta que não há Mind no pool
        result.IsSuccess.ShouldBeFalse();

        // Verifica se logou o aviso de segurança
        _engineMock.AppLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("possess")), // "essence they do not possess"
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Execute_WhenTargetInvulnerable_ShouldLogAndSkip()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);

        // Target Invulnerável
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
        var ability = CreateAbility("Nuke");
        ability.Effects = new() { effectDef };
        ability.TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } };

        _targetMock.ResolveTargets(Arg.Any<TargetingRule>(), source, state, Arg.Any<AbilityTargets>())
            .Returns(new List<Combatant> { target });

        
        _costMock.CalculateFinalCosts(
            Arg.Any<CombatPlayer>(),
            ability,
            Arg.Any<List<Combatant>>(),
            Arg.Any<AbilityTargets>())
            .Returns(new FinalAbilityCosts());

        _essenceMock.HasEnoughEssence(
            Arg.Any<CombatPlayer>(),
            Arg.Any<List<EssenceAmount>>())
            .Returns(true);

        var action = new ExecuteAbilityAction(
            ability,
            source,
            new AbilityTargets(),
            new Dictionary<EssenceType, int>());

        // ACT
        var result = action.Execute(_engineMock, state);

        // ASSERT
        _battleLogService.Received(1)
            .Log(Arg.Is<string>(s => s.Contains("is invulnerable")));
        _handlerMock.DidNotReceive().Apply(
            Arg.Any<EffectDefinition>(),
            Arg.Any<Combatant>(),
            Arg.Any<Combatant>(),
            Arg.Any<GameState>(),
            Arg.Any<CombatActionResult>());
    }

    // --- Helpers  ---
    private Combatant CreateCombatant(int id, int ownerId)
    {
        return new Combatant
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"C{id}",
            RaceId = "RACE_DEFAULT", // Campo Obrigatório adicionado
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
    }

    private AbilityDefinition CreateAbility(string id)
    {
        return new AbilityDefinition
        {
            Id = id,
            Name = id, // Required            
        };
    }
}