using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
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
    private readonly ITriggerProcessor _triggerProcessorMock; // New Dependency
    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _resolutionServiceMock = Substitute.For<IDamageResolutionService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();

        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _resolutionServiceMock,
            _triggerProcessorMock);
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, DamageCategory.Physical, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    [InlineData(DeliveryMethod.Ranged, DamageCategory.Physical, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    [InlineData(DeliveryMethod.Spell, DamageCategory.Magical, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    [InlineData(DeliveryMethod.Passive, DamageCategory.Magical, StatType.Attack, 0f, StatType.MagicDefense, 5f, 5)]
    public void Apply_DamageEffect_ShouldReduceTargetHP_BasedOnDeliveryMethod(
        DeliveryMethod delivery, DamageCategory damageCategory, StatType sourceStat, float sourceStatValue,
        StatType targetStat, float targetStatValue, int expectedDamage)
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = delivery,
            DamageCategory = damageCategory,
            ScalingFactor = 1.0f,
            BaseAmount = (delivery == DeliveryMethod.Passive) ? 5f : 0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, BaseStats = new BaseStats() };
        var gameState = new GameState(); // Dummy state required for signature

        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>(),
                AbsorbedDamage = 0
            });

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(50 - expectedDamage);
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

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };
        var gameState = new GameState();

        // Attack: 10
        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        // Defense: 999 (Would block everything if not True Damage)
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(999f);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>()
            });

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState);

        // 3. ASSERT
        // Should deal full 10 damage
        target.CurrentHP.ShouldBe(90);
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
        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(0f);

        var simulatedResult = new DamageResolutionResult
        {
            FinalDamageToApply = 5, // Only 5 gets through
            AbsorbedDamage = 5
        };

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(simulatedResult);

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(95);
        _resolutionServiceMock.Received(1).ResolveDamage(10f, effectDef, source, target);
    }

    [Fact]
    public void Apply_WhenDamageIsDealt_ShouldNotifyTriggerProcessor()
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

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(0f);

        // Simulate full damage application
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10f });

        // 2. ACT
        _handler.Apply(effectDef, source, target, gameState);

        // 3. ASSERT
        // Verify that generic damage triggers were fired
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target && c.Value == 10f));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_RECEIVE_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target && c.Value == 10f));

        // Verify that specific delivery trigger (Melee) was fired
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_MELEE_ATTACK,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));

        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_RECEIVE_MELEE_ATTACK,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));
    }
}