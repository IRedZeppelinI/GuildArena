using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.ValueObjects; // DamageResolutionResult
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
    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _resolutionServiceMock = Substitute.For<IDamageResolutionService>();

        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _resolutionServiceMock);
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
            ScalingFactor = 1.0f,
            BaseAmount = (delivery == DeliveryMethod.Passive) ? 5f : 0f, // Base 5 para passive
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, BaseStats = new BaseStats() };

        // Mock dos Stats (Para testar se o Handler escolhe o stat certo)
        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        // Mock da Resolução (Pass-through)
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>(), // Devolve o 1º argumento (dano mitigado)
                AbsorbedDamage = 0
            });

        // 2. ACT
        _handler.Apply(effectDef, source, target);

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
            DamageType = DamageType.True, // True Damage
            ScalingFactor = 1.0f,
            BaseAmount = 0,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };

        // Ataque: 10
        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        // Defesa: 999 (Seria imune se não fosse True Damage)
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(999f);

        // Mock Resolução (Pass-through)
        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(call => new DamageResolutionResult
            {
                FinalDamageToApply = call.Arg<float>()
            });

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // Deve entrar 10 de dano (ignora defesa)
        target.CurrentHP.ShouldBe(90);
    }

    [Fact]
    public void Apply_WhenServiceReportsAbsorption_ShouldLogAndReduceOnlyRemainingDamage()
    {
        // 1. ARRANGE
        // Cenário: Dano bruto 10, mas Barreira absorve 5.
        var effectDef = new EffectDefinition
        {
            TargetRuleId = "T_TestTarget",
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Melee,
            DamageType = DamageType.Martial,
            ScalingFactor = 1.0f
        };
        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 100, BaseStats = new BaseStats() };

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(0f);

        // Mock do Serviço: Simula que 5 foram absorvidos
        var simulatedResult = new DamageResolutionResult
        {
            FinalDamageToApply = 5, // Só passam 5
            AbsorbedDamage = 5
        };

        _resolutionServiceMock.ResolveDamage(Arg.Any<float>(), effectDef, source, target)
            .Returns(simulatedResult);

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // HP desce apenas 5
        target.CurrentHP.ShouldBe(95);

        // O serviço foi chamado com o dano total (10)
        _resolutionServiceMock.Received(1).ResolveDamage(10f, effectDef, source, target);
    }
}