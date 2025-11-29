using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Enums;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat;

public class CombatEngineTests
{
    private readonly ILogger<CombatEngine> _logger;
    private readonly ICooldownCalculationService _cooldownMock;
    private readonly ICostCalculationService _costMock;
    private readonly IEssenceService _essenceMock;
    private readonly IEffectHandler _handlerMock;
    private readonly ITargetResolutionService _targetServiceMock;
    private readonly IStatusConditionService _statusServiceMock;
    private readonly IHitChanceService _hitChanceServiceMock;
    private readonly IRandomProvider _randomMock;
    private readonly IStatCalculationService _statServiceMock;
    private readonly ITriggerProcessor _triggerProcessorMock;

    public CombatEngineTests()
    {
        _logger = Substitute.For<ILogger<CombatEngine>>();
        _cooldownMock = Substitute.For<ICooldownCalculationService>();
        _costMock = Substitute.For<ICostCalculationService>();
        _essenceMock = Substitute.For<IEssenceService>();
        _handlerMock = Substitute.For<IEffectHandler>();
        _targetServiceMock = Substitute.For<ITargetResolutionService>();
        _statusServiceMock = Substitute.For<IStatusConditionService>();
        _hitChanceServiceMock = Substitute.For<IHitChanceService>();
        _randomMock = Substitute.For<IRandomProvider>();
        _statServiceMock = Substitute.For<IStatCalculationService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();

        // Setup Base: Handler suporta Dano
        _handlerMock.SupportedType.Returns(EffectType.DAMAGE);

        // Setup Base: Permite ação por defeito (sem CC)
        _statusServiceMock.CheckStatusConditions(Arg.Any<Combatant>(), Arg.Any<AbilityDefinition>())
            .Returns(ActionStatusResult.Allowed);

        // Setup Base por defeito acerta sempre
        _hitChanceServiceMock.CalculateHitChance(Arg.Any<Combatant>(), Arg.Any<Combatant>(), Arg.Any<EffectDefinition>())
            .Returns(1.0f);
        _randomMock.NextDouble().Returns(0.0);

        // Setup Base: MaxActions = 1
        _statServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.MaxActions).Returns(1f);
    }

    private CombatEngine CreateEngine(IEnumerable<IEffectHandler>? handlers = null)
    {
        var handlerList = handlers ?? new[] { _handlerMock };
        return new CombatEngine(
            handlerList,
            _logger,
            _cooldownMock,
            _costMock,
            _essenceMock,
            _targetServiceMock,
            _statusServiceMock,
            _hitChanceServiceMock,
            _randomMock,
            _statServiceMock,
            _triggerProcessorMock
        );
    }

    // --- TESTES DE HAPPY PATH ---

    [Fact]
    public void ExecuteAbility_HappyPath_ShouldPayCostsAndExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Fireball",
            ActionPointCost = 1,
            Effects = new() { new() { Type = EffectType.DAMAGE, TargetRuleId = "T1" } },
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy, Count = 1 } }
        };

        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);
        var player = new CombatPlayer { PlayerId = 1 };
        var gameState = new GameState { Combatants = new() { source, target }, Players = new() { player } };

        var targetsInput = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 2 } } } };
        var payment = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 2 } };

        // Mocks de Sucesso
        _cooldownMock.GetFinalCooldown(source, ability).Returns(0);
        var calculatedCost = new FinalAbilityCosts
        {
            HPCost = 10,
            EssenceCosts = new() { new() { Type = EssenceType.Vigor, Amount = 2 } }
        };
        _costMock.CalculateFinalCosts(player, ability, Arg.Any<List<Combatant>>()).Returns(calculatedCost);
        _essenceMock.HasEnoughEssence(player, calculatedCost.EssenceCosts).Returns(true);
        _targetServiceMock.ResolveTargets(Arg.Any<TargetingRule>(), source, gameState, targetsInput)
            .Returns(new List<Combatant> { target });

        // ACT
        engine.ExecuteAbility(gameState, ability, source, targetsInput, payment);

        // ASSERT
        // Pagou Essence?
        _essenceMock.Received(1).ConsumeEssence(player, payment);
        // Pagou HP?
        source.CurrentHP.ShouldBe(90);
        // Gastou Ação?
        source.ActionsTakenThisTurn.ShouldBe(1);
        // Executou Efeito?
        _handlerMock.Received(1).Apply(Arg.Any<EffectDefinition>(), source, target, gameState);
    }

    [Fact]
    public void ExecuteAbility_ShouldFireOnAbilityCastTrigger_WithCorrectTags()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition
        {
            Id = "Fireball",
            Name = "Fireball",
            Tags = new List<string> { "Fire", "Spell" },
            TargetingRules = new()
        };
        var source = CreateCombatant(1, 1);

        // Mocks para passar as validações
        _statServiceMock.GetStatValue(source, StatType.MaxActions).Returns(1);
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>()).Returns(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(Arg.Any<CombatPlayer>(), Arg.Any<List<EssenceAmount>>()).Returns(true);

        // ACT
        engine.ExecuteAbility(new GameState { Players = new() { new CombatPlayer { PlayerId = 1 } } }, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_ABILITY_CAST,
            Arg.Is<TriggerContext>(ctx =>
                ctx.Source == source &&
                ctx.Tags.Contains("Fire") &&
                ctx.Tags.Contains("Spell")
            ));
    }

    // --- TESTES: ACTION POINTS ---

    [Fact]
    public void ExecuteAbility_WhenOutOfActions_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "A1", Name = "Attack", ActionPointCost = 1 };
        var source = CreateCombatant(1, 1);

        // Simular que já gastou a ação
        source.ActionsTakenThisTurn = 1;
        _statServiceMock.GetStatValue(source, StatType.MaxActions).Returns(1f);

        // ACT
        engine.ExecuteAbility(new GameState(), ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _costMock.DidNotReceive().CalculateFinalCosts(Arg.Any<CombatPlayer>(), Arg.Any<AbilityDefinition>(), Arg.Any<List<Combatant>>());

        _logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("has no actions left")), null, Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ExecuteAbility_FreeAction_ShouldExecuteEvenIfExhausted()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition
        {
            Id = "Free",
            Name = "Free",
            ActionPointCost = 0, // Custo Zero
            TargetingRules = new()
        };
        var source = CreateCombatant(1, 1);

        source.ActionsTakenThisTurn = 1; // Já gastou tudo
        _statServiceMock.GetStatValue(source, StatType.MaxActions).Returns(1f);

        // Configurar mocks mínimos para passar
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>()).Returns(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(Arg.Any<CombatPlayer>(), Arg.Any<List<EssenceAmount>>()).Returns(true);

        // ACT
        engine.ExecuteAbility(new GameState { Players = new() { new CombatPlayer { PlayerId = 1 } } }, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _cooldownMock.Received().GetFinalCooldown(source, ability);
    }

    // --- TESTES: STATUS CONDITIONS (CC) ---

    [Fact]
    public void ExecuteAbility_WhenStatusServiceReturnsStunned_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "A1", Name = "Attack" };
        var source = CreateCombatant(1, 1);

        // Configurar Mock para bloquear
        _statusServiceMock.CheckStatusConditions(source, ability).Returns(ActionStatusResult.Stunned);

        // ACT
        engine.ExecuteAbility(new GameState(), ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _costMock.DidNotReceive().CalculateFinalCosts(Arg.Any<CombatPlayer>(), Arg.Any<AbilityDefinition>(), Arg.Any<List<Combatant>>());

        _logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Reason: Stunned")), null, Arg.Any<Func<object, Exception?, string>>());
    }

    // --- TESTES: EVASION (HIT/MISS) ---

    [Fact]
    public void ExecuteAbility_WhenAttackMisses_ShouldNotApplyEffect()
    {
        // ARRANGE
        var engine = CreateEngine();
        var effectDef = new EffectDefinition
        {
            CanBeEvaded = true,
            TargetRuleId = "T1"
        };
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Attack",
            Effects = new() { effectDef },
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } }
        };

        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);
        var gameState = new GameState { Combatants = new() { source, target }, Players = new() { new CombatPlayer { PlayerId = 1 } } };

        _targetServiceMock.ResolveTargets(Arg.Any<TargetingRule>(), source, gameState, Arg.Any<AbilityTargets>())
            .Returns(new List<Combatant> { target });

        // Mocks de Sucesso
        _statServiceMock.GetStatValue(source, StatType.MaxActions).Returns(1);
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>()).Returns(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(Arg.Any<CombatPlayer>(), Arg.Any<List<EssenceAmount>>()).Returns(true);

        // CONFIGURAR MISS
        // Chance: 50%
        // Roll: 0.6 (Maior que a chance -> Miss)
        _hitChanceServiceMock.CalculateHitChance(source, target, effectDef).Returns(0.5f);
        _randomMock.NextDouble().Returns(0.6);

        // ACT
        engine.ExecuteAbility(gameState, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        // Handler NÃO deve ser chamado
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>(), Arg.Any<GameState>());

        // Log de Miss deve aparecer
        _logger.Received().Log(LogLevel.Information, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("MISSED")), null, Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ExecuteAbility_InvulnerableTarget_ShouldIgnoreEffect()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Attack",
            Effects = new() { new() { Type = EffectType.DAMAGE, TargetRuleId = "T1" } },
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } }
        };

        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 2);

        // Configurar estado diretamente
        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_ICEBLOCK",
            ActiveStatusEffects = new List<StatusEffectType> { StatusEffectType.Invulnerable }
        });

        var gameState = new GameState { Combatants = new() { source, target }, Players = new() { new CombatPlayer { PlayerId = 1 } } };

        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), Arg.Any<AbilityDefinition>(), Arg.Any<List<Combatant>>()).Returns(new FinalAbilityCosts());
        _essenceMock.HasEnoughEssence(Arg.Any<CombatPlayer>(), Arg.Any<List<EssenceAmount>>()).Returns(true);
        _targetServiceMock.ResolveTargets(Arg.Any<TargetingRule>(), source, gameState, Arg.Any<AbilityTargets>()).Returns(new List<Combatant> { target });

        // ACT
        engine.ExecuteAbility(gameState, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), source, target, Arg.Any<GameState>());

        _logger.Received().Log(LogLevel.Information, Arg.Any<EventId>(),
            Arg.Any<object>(), null, Arg.Any<Func<object, Exception?, string>>());
    }

    // --- TESTES DE VALIDAÇÃO (HP/Essence/Cooldown) ---

    [Fact]
    public void ExecuteAbility_OnCooldown_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "A1", Name = "Test" };
        var source = CreateCombatant(1, 1);
        source.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "A1", TurnsRemaining = 1 });

        // ACT
        engine.ExecuteAbility(new GameState(), ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _costMock.DidNotReceive().CalculateFinalCosts(Arg.Any<CombatPlayer>(), Arg.Any<AbilityDefinition>(), Arg.Any<List<Combatant>>());
    }

    [Fact]
    public void ExecuteAbility_NotEnoughHP_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "Suicide", Name = "Die" };
        var source = CreateCombatant(1, 1);
        source.CurrentHP = 10;

        var highCost = new FinalAbilityCosts { HPCost = 20, EssenceCosts = new() };
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>()).Returns(highCost);

        // ACT
        engine.ExecuteAbility(new GameState { Players = new() { new CombatPlayer { PlayerId = 1 } } }, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>(), Arg.Any<GameState>());
    }

    [Fact]
    public void ExecuteAbility_NotEnoughEssence_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "Rich", Name = "Rich" };
        var source = CreateCombatant(1, 1);
        var player = new CombatPlayer { PlayerId = 1 };

        var cost = new FinalAbilityCosts { EssenceCosts = new() { new() { Type = EssenceType.Light, Amount = 5 } } };
        _costMock.CalculateFinalCosts(player, ability, Arg.Any<List<Combatant>>()).Returns(cost);
        _essenceMock.HasEnoughEssence(player, cost.EssenceCosts).Returns(false);

        // ACT
        engine.ExecuteAbility(new GameState { Players = new() { player } }, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>(), Arg.Any<GameState>());
    }

    // --- HELPER LOCAL ---
    private Combatant CreateCombatant(int id, int ownerId)
    {
        return new Combatant
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"C{id}",
            CurrentHP = 100, 
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
    }
}