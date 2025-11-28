using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class HitChanceServiceTests
{
    private readonly IStatCalculationService _statServiceMock;
    private readonly HitChanceService _service;

    // Constantes de Tuning (Replicadas do Serviço para validar a lógica)
    private const float BaseChance = 1.0f;
    private const float OffenseFactor = 0.005f;
    private const float DefenseFactor = 0.01f;
    private const float LevelDeltaFactor = 0.02f;
    private const float MinChance = 0.05f;
    private const float MaxChance = 1.0f;

    public HitChanceServiceTests()
    {
        _statServiceMock = Substitute.For<IStatCalculationService>();
        _service = new HitChanceService(_statServiceMock);
    }

    [Fact]
    public void Calculate_CanBeEvadedFalse_ShouldAlwaysReturnOne()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            CanBeEvaded = false,
            TargetRuleId = "T_Any"
        };
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        chance.ShouldBe(1.0f);
    }

    [Fact]
    public void Calculate_Melee_StandardValues_ShouldApplyFormula()
    {
        // ARRANGE
        var attackVal = 20f;
        var agilityVal = 20f;

        var source = CreateCombatant(1, 1); // Lv 1
        var target = CreateCombatant(2, 1); // Lv 1

        SetupStat(source, StatType.Attack, attackVal);
        SetupStat(target, StatType.Agility, agilityVal);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_Melee"
        };

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // Expected: 1.0 + (20 * 0.005) - (20 * 0.01) = 1.0 + 0.1 - 0.2 = 0.9
        var expected = BaseChance + (attackVal * OffenseFactor) - (agilityVal * DefenseFactor);
        chance.ShouldBe(expected, tolerance: 0.001f);
    }

    [Theory]
    [InlineData(DeliveryMethod.Melee, StatType.Attack, StatType.Agility)]
    [InlineData(DeliveryMethod.Ranged, StatType.Agility, StatType.Agility)]
    [InlineData(DeliveryMethod.Spell, StatType.Magic, StatType.MagicDefense)]
    public void Calculate_DeliveryMethods_ShouldUseCorrectStats(
        DeliveryMethod delivery, StatType expectedOffenseStat, StatType expectedDefenseStat)
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);

        // Configurar valores arbitrários para garantir que o serviço chamou o stat certo
        SetupStat(source, expectedOffenseStat, 100);
        SetupStat(target, expectedDefenseStat, 50);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = delivery,
            TargetRuleId = "T_Test"
        };

        // ACT
        _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // Verificar se o serviço pediu os stats corretos ao StatService
        _statServiceMock.Received(1).GetStatValue(source, expectedOffenseStat);
        _statServiceMock.Received(1).GetStatValue(target, expectedDefenseStat);
    }

    [Fact]
    public void Calculate_LevelAdvantage_ShouldIncreaseChance()
    {
        // ARRANGE
        var sourceLvl = 5;
        var targetLvl = 1;
        var agilityVal = 20f;

        var source = CreateCombatant(1, sourceLvl);
        var target = CreateCombatant(2, targetLvl);

        // Target com Agility para baixar a chance base abaixo de 100%
        // Senão o bónus de nível seria cortado pelo Cap de 1.0
        SetupStat(target, StatType.Agility, agilityVal);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_LevelTest"
        };

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // Base - PenaltyAgility + BonusLevel
        var expected = BaseChance - (agilityVal * DefenseFactor) + ((sourceLvl - targetLvl) * LevelDeltaFactor);
        chance.ShouldBe(expected, tolerance: 0.001f);
    }

    [Fact]
    public void Calculate_LevelDisadvantage_ShouldDecreaseChance()
    {
        // ARRANGE
        var sourceLvl = 1;
        var targetLvl = 6;

        var source = CreateCombatant(1, sourceLvl);
        var target = CreateCombatant(2, targetLvl);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_Disadvantage"
        };

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // Base + PenaltyLevel (que será negativo)
        var expected = BaseChance + ((sourceLvl - targetLvl) * LevelDeltaFactor);
        chance.ShouldBe(expected, tolerance: 0.001f);
    }

    [Fact]
    public void Calculate_ShouldClampToMinChance()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        // Defesa massiva para forçar negativo
        SetupStat(target, StatType.Agility, 2000);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_ClampMin"
        };

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        chance.ShouldBe(MinChance);
    }

    [Fact]
    public void Calculate_ShouldClampToMaxChance()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        // Ataque massivo
        SetupStat(source, StatType.Attack, 2000);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_ClampMax"
        };

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        chance.ShouldBe(MaxChance);
    }

    // --- Helpers ---

    private Combatant CreateCombatant(int id, int level)
    {
        return new Combatant
        {
            Id = id,
            OwnerId = 1,
            Name = $"C{id}",
            Level = level,
            CurrentHP = 100, 
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
    }

    private void SetupStat(Combatant c, StatType type, float value)
    {
        _statServiceMock.GetStatValue(c, type).Returns(value);
    }
}