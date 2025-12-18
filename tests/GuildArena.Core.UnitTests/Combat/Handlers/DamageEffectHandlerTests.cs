using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class DamageEffectHandlerTests
{
    private readonly IStatCalculationService _statCalculationServiceMock;
    private readonly ILogger<DamageEffectHandler> _loggerMock;
    private readonly IDamageResolutionService _resolutionServiceMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly DamageEffectHandler _handler;
    private readonly IBattleLogService _battleLogService;

    public DamageEffectHandlerTests()
    {
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _resolutionServiceMock = Substitute.For<IDamageResolutionService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _battleLogService = Substitute.For<IBattleLogService>();

        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _resolutionServiceMock,
            _triggerProcessorMock,
            _battleLogService);
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, DamageCategory.Physical, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    [InlineData(DeliveryMethod.Ranged, DamageCategory.Physical, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    [InlineData(DeliveryMethod.Spell, DamageCategory.Magical, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    public void Apply_DamageEffect_ShouldReduceTargetHP_AndLog_BasedOnDelivery(
        DeliveryMethod delivery, DamageCategory damageCategory, StatType sourceStat,
        float sourceStatValue, StatType targetStat, float targetStatValue, int expectedDamage)
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = delivery,
            DamageCategory = damageCategory,
            ScalingFactor = 1.0f,
            BaseAmount = 0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "RACE_B",
            CurrentHP = 50,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, sourceStat)
            .Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat)
            .Returns(targetStatValue);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>(),
                AbsorbedDamage = 0
            });

        var actionResult = new CombatActionResult();

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState, actionResult);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(50 - expectedDamage);
        _battleLogService.
            Received(1).Log(
            Arg.Is<string>(s => s.Contains($"took {expectedDamage} damage")));
    }

    [Fact]
    public void Apply_TrueDamage_ShouldIgnoreDefense()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.True,
            ScalingFactor = 1.0f,
            BaseAmount = 0,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(999f);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>()
            });

        var actionResult = new CombatActionResult();

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState, actionResult);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(90);
        _battleLogService.
            Received(1).Log(Arg.Is<string>(s => s.Contains("took 10 damage")));
    }

    [Fact]
    public void Apply_WhenServiceReportsAbsorption_ShouldLogAndReduceOnlyRemainingDamage()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            TargetRuleId = "T_TestTarget",
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.Physical,
            ScalingFactor = 1.0f
        };
        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(0f);

        var simulatedResult = new DamageResolutionResult
        {
            FinalDamageToApply = 5,
            AbsorbedDamage = 5
        };

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(simulatedResult);

        var actionResult = new CombatActionResult();

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState, actionResult);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(95);
        _battleLogService.
            Received(1).Log(Arg.Is<string>(s => s.Contains("took 5 damage")));
        _resolutionServiceMock.Received(1).ResolveDamage(10f, effectDef, source, target);
    }

    [Fact]
    public void Apply_WhenDamageIsDealt_ShouldNotifyTriggerProcessor_WithAllTags()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.Physical,
            ScalingFactor = 1.0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(0f);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10f });

        var actionResult = new CombatActionResult();

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState, actionResult);

        // 3. ASSERT
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target && c.Value == 10f));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_RECEIVE_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target && c.Value == 10f));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_MELEE_ATTACK,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_PHYSICAL_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_RECEIVE_PHYSICAL_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));
    }
}