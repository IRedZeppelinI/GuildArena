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
    private readonly IBattleLogService _battleLogService;
    private readonly IDeathService _deathServiceMock; 

    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _resolutionServiceMock = Substitute.For<IDamageResolutionService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _battleLogService = Substitute.For<IBattleLogService>();
        _deathServiceMock = Substitute.For<IDeathService>();

        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _resolutionServiceMock,
            _triggerProcessorMock,
            _battleLogService,
            _deathServiceMock 
            );
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, DamageCategory.Physical, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    [InlineData(DeliveryMethod.Ranged, DamageCategory.Physical, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    [InlineData(DeliveryMethod.Spell, DamageCategory.Magical, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    public void Apply_DamageEffect_ShouldReduceTargetHP_AndLog_BasedOnDelivery(
        DeliveryMethod delivery, DamageCategory damageCategory, StatType sourceStat,
        float sourceStatValue, StatType targetStat, float targetStatValue, int expectedDamage)
    {
        // ARRANGE
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
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 50,
            BaseStats = new()
        };
        var gameState = new GameState();

        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = expectedDamage });

        // ACT
        _handler.Apply(effectDef, source, target, gameState, new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(50 - expectedDamage);

        _battleLogService.Received(1)
            .Log(Arg.Is<string>(s => s.Contains($"took {expectedDamage} damage")));

        // Verifica se chamou o trigger genérico de dano
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_DAMAGE,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));
    }

    [Fact]
    public void Apply_TrueDamage_ShouldIgnoreDefense()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.True,
            ScalingFactor = 1.0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Target",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10f });

        // ACT
        _handler.Apply
            (effectDef, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(90);
    }

    [Fact]
    public void Apply_WhenServiceReportsAbsorption_ShouldLogAndReduceOnlyRemainingDamage()
    {
        // ARRANGE
        var effectDef = new EffectDefinition { TargetRuleId = "T1" };
        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };

        var simulatedResult = new DamageResolutionResult { FinalDamageToApply = 5, AbsorbedDamage = 5 };
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target).Returns(simulatedResult);

        // ACT
        _handler.Apply
            (effectDef, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(95);
        _battleLogService.Received(1)
            .Log(Arg.Is<string>(s => s.Contains("took 5 damage")));
    }

    [Fact]
    public void Apply_WhenDamageIsDealt_ShouldNotifyTriggerProcessor_WithAllTags()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.Physical,
            ScalingFactor = 1.0f,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Attacker",
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Defender",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10f });

        // ACT
        _handler.Apply
            (effectDef, source, target, new GameState(), new CombatActionResult());

        // ASSERT - Reposição dos teus asserts detalhados originais
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

    // --- NOVOS TESTES (Morte) ---

    [Fact]
    public void Apply_WhenTargetDies_ShouldDelegateToDeathService()
    {
        // ARRANGE
        var effectDef = new EffectDefinition { TargetRuleId = "T1" };
        var source = new Combatant
        {
            Id = 1,
            Name = "Killer",
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        // Target com 10 HP
        var target = new Combatant
        {
            Id = 2,
            Name = "Victim",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 10,
            BaseStats = new()
        };
        var gameState = new GameState();

        // Vai levar 10 de dano -> Fica com 0 HP
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10 });

        // ACT
        _handler.Apply(effectDef, source, target, gameState, new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(0);

        // Verifica se o serviço de morte foi chamado
        _deathServiceMock.Received(1).ProcessDeathIfApplicable(target, source, gameState);

        // Garante que o Handler NÃO tenta fazer o trabalho do DeathService (não dispara ON_DEATH)
        _triggerProcessorMock.DidNotReceive()
            .ProcessTriggers(ModifierTrigger.ON_DEATH, Arg.Any<TriggerContext>());
    }

    [Fact]
    public void Apply_WhenTargetSurvives_ShouldCallDeathService()
    {
        // ARRANGE
        var effectDef = new EffectDefinition { TargetRuleId = "T1" };
        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "A",
            MaxHP = 100,
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Survivor",
            RaceId = "B",
            MaxHP = 100,
            CurrentHP = 20,
            BaseStats = new()
        };

        // Leva 10 dano -> Sobra 10 (Sobrevive)
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(new DamageResolutionResult { FinalDamageToApply = 10 });

        // ACT
        _handler.Apply(effectDef, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(10);
        
        _deathServiceMock.Received(1).ProcessDeathIfApplicable(target, source, Arg.Any<GameState>());
    }
}