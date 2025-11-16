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
using System.Collections.Generic;

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
        _damageModServiceMock = Substitute.For<IDamageModificationService>(); // <-- Mockar a nova interface

        // Injetar as dependências corretas (3)
        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _damageModServiceMock); // <-- Passar o mock
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, DamageType.Physical, StatType.Attack, 10f, StatType.Defense, 2f, 8)]
    [InlineData(DeliveryMethod.Ranged, DamageType.Physical, StatType.Agility, 12f, StatType.Defense, 2f, 10)]
    [InlineData(DeliveryMethod.Spell, DamageType.Magic, StatType.Magic, 15f, StatType.MagicDefense, 5f, 10)]
    [InlineData(DeliveryMethod.Passive, DamageType.Nature, StatType.Attack, 0f, StatType.MagicDefense, 5f, 5)]
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
        // Dizemos-lhe: "Quando fores chamado, apenas devolve o dano mitigado que recebeste."
        // (Isto isola o teste - não estamos a testar modifiers aqui)
        _damageModServiceMock.CalculateModifiedValue(
            (float)expectedDamage, // O dano mitigado (ex: 8)
            effectDef,
            source,
            target)
            .Returns((float)expectedDamage); // Devolve o mesmo valor (8)

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
            DamageType = DamageType.Physical,
            ScalingFactor = 1.0f,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 50, BaseStats = new BaseStats() };

        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(5f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(10f);

        // Dano mitigado = 5 - 10 = -5
        // O handler vai calcular 1 (mínimo)
        _damageModServiceMock.CalculateModifiedValue(-5f, effectDef, source, target)
            .Returns(-5f); // O ModService devolve -5 (não sabe do mínimo)

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // O handler aplica o mínimo de 1, independentemente do que o ModService diz
        target.CurrentHP.ShouldBe(49);
    }

    [Fact]
    public void Apply_WithDamageTagModifier_ShouldCallDamageModServiceCorrectly()
    {
        // 1. ARRANGE
        // Este teste agora SÓ verifica se o DamageModService é chamado
        // com o dano mitigado correto.
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Spell,
            DamageType = DamageType.Nature,
            ScalingFactor = 1.0f,
            Tags = new() { "Magic", "Nature" },
            TargetRuleId = "T_TestTarget"
        };

        // Mocks de Stats
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.Magic).Returns(100f);
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.MagicDefense).Returns(20f);

        // Dano mitigado (Fase 1) = 100 - 20 = 80

        // Mock do DamageModService (Fase 2)
        // Dizemos-lhe: "Quando fores chamado com 80 de dano, devolve 96 (80 * 1.20)"
        _damageModServiceMock.CalculateModifiedValue(80f, effectDef, Arg.Any<Combatant>(), Arg.Any<Combatant>())
            .Returns(96f);

        // Combatentes
        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 200, BaseStats = new BaseStats() };

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // O handler aplicou o valor final que o ModService lhe deu?
        // HP Final = 200 - 96 = 104
        target.CurrentHP.ShouldBe(104);

        // Verificação extra: O ModService foi chamado com o dano mitigado CORRETO (80)?
        _damageModServiceMock.Received(1).CalculateModifiedValue(
            80f,
            effectDef,
            source,
            target
        );
    }
}