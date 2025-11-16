using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Abstractions.Repositories; 
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
    private readonly IModifierDefinitionRepository _modifierRepoMock; 
    private readonly DamageEffectHandler _handler;

    
    private readonly Dictionary<string, ModifierDefinition> _mockModifierDb;
    private readonly ModifierDefinition _natureBuff; 

    public DamageEffectHandlerTests()
    {
        // ARRANGE Global 
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();
        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>(); 

        // --- Setup do Mock do Repositório ---
        _natureBuff = new ModifierDefinition
        {
            Id = "MOD_NATURE_ADEPT",
            Name = "+20% Dano Natureza",
            DamageModifications = new() {
                new() { RequiredTag = "Nature", Type = ModificationType.PERCENTAGE, Value = 0.20f }
            }
        };

        // Criamos a nossa "BD falsa"
        _mockModifierDb = new Dictionary<string, ModifierDefinition>
        {
            { _natureBuff.Id, _natureBuff }
        };

        // CORREÇÃO: Configurar o GetAllDefinitions()
        _modifierRepoMock.GetAllDefinitions().Returns(_mockModifierDb);
        // --- Fim do Setup do Mock ---

        // Injetar as dependências corretas
        _handler = new DamageEffectHandler(
            _statCalculationServiceMock,
            _loggerMock,
            _modifierRepoMock);
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

        _statCalculationServiceMock.GetStatValue(source, sourceStat).Returns(sourceStatValue);
        _statCalculationServiceMock.GetStatValue(target, targetStat).Returns(targetStatValue);

        // (O _modifierRepoMock já está configurado no construtor)

        //  ACT
        _handler.Apply(effectDef, source, target);

        //  ASSERT
        target.CurrentHP.ShouldBe(50 - expectedDamage);
    }

    [Fact]
    public void Apply_DamageEffect_ShouldDealMinimumOneDamage()
    {
        //  ARRANGE
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
        // (O _modifierRepoMock já está configurado no construtor)

        //  ACT
        _handler.Apply(effectDef, source, target);

        //  ASSERT
        target.CurrentHP.ShouldBe(49);
    }

    [Fact]
    public void Apply_WithDamageTagModifier_ShouldIncreaseFinalDamage()
    {
        // 1. ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            Delivery = DeliveryMethod.Spell,
            DamageType = DamageType.Nature,
            ScalingFactor = 1.0f,
            Tags = new() { "Magic", "Nature" }, // <-- A TAG IMPORTANTE
            TargetRuleId = "T_TestTarget"
        };

        // Configurar Mocks
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.Magic).Returns(100f);
        _statCalculationServiceMock.GetStatValue(Arg.Any<Combatant>(), StatType.MagicDefense).Returns(20f);

        // (O _modifierRepoMock já está configurado no construtor e 
        //  sabe o que é "MOD_NATURE_ADEPT" através do _natureBuff)

        // Criar Combatentes
        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CurrentHP = 200, BaseStats = new BaseStats() };

        // Aplicar o Buff ao Atacante
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_NATURE_ADEPT" });

        // 2. ACT
        _handler.Apply(effectDef, source, target);

        // 3. ASSERT
        // Dano Base = 100 (Ataque) - 20 (Defesa) = 80
        // Bónus % = +20% (porque a tag "Nature" deu match)
        // Dano Final = 80 * 1.20 = 96
        // HP Final = 200 - 96 = 104
        target.CurrentHP.ShouldBe(104);
    }
}