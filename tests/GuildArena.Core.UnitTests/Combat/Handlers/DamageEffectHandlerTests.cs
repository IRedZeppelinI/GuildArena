using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
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
    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        // ARRANGE Global 
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _handler = new DamageEffectHandler(_statCalculationServiceMock, _loggerMock);
    }

    [Theory]
    // Cenário 1: Melee (Attack vs Defense)
    [InlineData(DeliveryMethod.Melee, DamageType.Physical, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    // Cenário 2: Ranged (Agility vs Defense)
    [InlineData(DeliveryMethod.Ranged, DamageType.Physical, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    // Cenário 3: Spell (Magic vs MagicDefense) <-- CORRIGIDO
    [InlineData(DeliveryMethod.Spell, DamageType.Magic, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    // Cenário 4: Passive (ignora stats) <-- CORRIGIDO
    [InlineData(DeliveryMethod.Passive, DamageType.Nature, StatType.Attack, 0f, StatType.MagicDefense, 5f, 5)]
    public void Apply_DamageEffect_ShouldReduceTargetHP_BasedOnDeliveryMethod(
        DeliveryMethod delivery, DamageType damageType, StatType sourceStat, float sourceStatValue,
        StatType targetStat, float targetStatValue, int expectedDamage)
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = delivery,
            DamageType = damageType, 
            ScalingStat = sourceStat, 
            ScalingFactor = 1.0f,
            BaseAmount = (delivery == DeliveryMethod.Passive) ? 5f : 0f
        };

        var source = new Combatant { Id = 1, Name = "Source", CalculatedStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, CalculatedStats = new BaseStats() };

        
        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);

        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        // HP Final = 50 - expectedDamage
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
            DamageType = DamageType.Physical,
            ScalingFactor = 1.0f
        };

        var source = new Combatant { Id = 1, Name = "Source", CalculatedStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, CalculatedStats = new BaseStats() };

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(5f); // 5 de Ataque
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(10f); // 10 de Defesa

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        // Dano = 5 - 10 = -5. Deve ser 1.
        // HP Final = 50 - 1 = 49
        target.CurrentHP.ShouldBe(49);
    }
}