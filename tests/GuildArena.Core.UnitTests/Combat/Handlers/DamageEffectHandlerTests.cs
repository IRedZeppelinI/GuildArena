using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class DamageEffectHandlerTests
{
    private readonly IStatCalculationService _statCalculationServiceMock;
    private readonly ILogger<DamageEffectHandler> _loggerMock;
    private readonly IDamageModificationService _damageModServiceMock;
    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        // ARRANGE Global 
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _damageModServiceMock = Substitute.For<IDamageModificationService>();

        // Injetar as dependências corretas
        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _damageModServiceMock);
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, DamageType.Martial, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    [InlineData(DeliveryMethod.Ranged, DamageType.Martial, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    [InlineData(DeliveryMethod.Spell, DamageType.Mystic, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    [InlineData(DeliveryMethod.Passive, DamageType.Primal, StatType.Attack, 0f, StatType.MagicDefense, 5f, 5)]
    public void Apply_DamageEffect_ShouldReduceTargetHP_BasedOnDeliveryMethod(
        DeliveryMethod delivery, DamageType damageType, StatType sourceStat, float sourceStatValue,
        StatType targetStat, float targetStatValue, int expectedDamage)
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = delivery,
            DamageType = damageType,
            ScalingStat = sourceStat,
            ScalingFactor = 1.0f,
            BaseAmount = (delivery == DeliveryMethod.Passive) ? 5f : 0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, BaseStats = new BaseStats() };

        // Configurar o mock do StatService (Fase 1 do dano)
        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        // Configurar o mock do DamageModService (Fase 2 do dano)
        _damageModServiceMock.CalculateModifiedValue(
            (float)expectedDamage,
            effectDef,
            source,
            target)
            .Returns((float)expectedDamage);

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(50 - expectedDamage);
    }

    [Fact]
    public void Apply_DamageEffect_ShouldDealMinimumOneDamage()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageType = DamageType.Martial,
            ScalingFactor = 1.0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, BaseStats = new BaseStats() };

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(5f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(10f);

        // Dano mitigado = 5 - 10 = -5
        _damageModServiceMock.CalculateModifiedValue(-5f, effectDef, source, target)
            .Returns(-5f);

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(49);
    }

    [Fact]
    public void Apply_WithDamageTagModifier_ShouldCallDamageModServiceCorrectly()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Spell,
            DamageType = DamageType.Primal,
            ScalingFactor = 1.0f,
            Tags = new() { "Magic", "Primal" },
            TargetRuleId = "T_TestTarget"
        };

        // Mocks de Stats
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.Magic).Returns(100f);
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.MagicDefense).Returns(20f);

        // Dano mitigado (Fase 1) = 100 - 20 = 80

        // Mock do DamageModService (Fase 2)
        _damageModServiceMock.CalculateModifiedValue(80f, effectDef, Arg.Any<Combatant>(), Arg.Any<Combatant>())
            .Returns(96f);

        // Combatentes
        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 200, BaseStats = new BaseStats() };

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        target.CurrentHP.ShouldBe(104);

        _damageModServiceMock.Received(1).CalculateModifiedValue(
            80f,
            effectDef,
            source,
            target
        );
    }

    [Fact]
    public void Apply_TrueDamage_ShouldIgnoreDefense()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageType = DamageType.True, // <--- O caso especial
            ScalingFactor = 1.0f,
            BaseAmount = 0,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };

        // Configurar Stats: O atacante tem 10 de Ataque
        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);

        // O alvo tem 999 de Defesa (se não fosse True damage, o dano seria 1)
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(999f);
        _statCalculationServiceMock.GetStatValue(target, StatType.MagicDefense).Returns(999f);

        // Mock do DamageModService (pass-through)
        _damageModServiceMock.CalculateModifiedValue(Arg.Any<float>(), effectDef, source, target)
            .Returns(x => x.Arg<float>()); // Retorna o que recebe

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // Se a defesa fosse usada, o dano seria 1 (minimo).
        // Como é True Damage, deve entrar os 10 completos.
        target.CurrentHP.ShouldBe(90); // 100 - 10
    }
}