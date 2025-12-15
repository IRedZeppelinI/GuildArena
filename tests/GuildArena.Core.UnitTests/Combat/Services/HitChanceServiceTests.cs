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

    // Constantes de Tuning
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
        var effect = new EffectDefinition
        {
            CanBeEvaded = false,
            TargetRuleId = "T_Any"
        };
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);

        var chance = _service.CalculateHitChance(source, target, effect);

        chance.ShouldBe(1.0f);
    }

    [Fact]
    public void Calculate_Melee_StandardValues_ShouldApplyFormula()
    {
        var attackVal = 20f;
        var agilityVal = 20f;

        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);

        SetupStat(source, StatType.Attack, attackVal);
        SetupStat(target, StatType.Agility, agilityVal);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_Melee"
        };

        var chance = _service.CalculateHitChance(source, target, effect);

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
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);

        SetupStat(source, expectedOffenseStat, 100);
        SetupStat(target, expectedDefenseStat, 50);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = delivery,
            TargetRuleId = "T_Test"
        };

        _service.CalculateHitChance(source, target, effect);

        _statServiceMock.Received(1).GetStatValue(source, expectedOffenseStat);
        _statServiceMock.Received(1).GetStatValue(target, expectedDefenseStat);
    }

    [Fact]
    public void Calculate_LevelAdvantage_ShouldIncreaseChance()
    {
        var sourceLvl = 5;
        var targetLvl = 1;
        var agilityVal = 20f;

        var source = CreateCombatant(1, sourceLvl);
        var target = CreateCombatant(2, targetLvl);

        SetupStat(target, StatType.Agility, agilityVal);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_LevelTest"
        };

        var chance = _service.CalculateHitChance(source, target, effect);

        var expected = BaseChance - (agilityVal * DefenseFactor) + ((sourceLvl - targetLvl) * LevelDeltaFactor);
        chance.ShouldBe(expected, tolerance: 0.001f);
    }

    [Fact]
    public void Calculate_LevelDisadvantage_ShouldDecreaseChance()
    {
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

        var chance = _service.CalculateHitChance(source, target, effect);

        var expected = BaseChance + ((sourceLvl - targetLvl) * LevelDeltaFactor);
        chance.ShouldBe(expected, tolerance: 0.001f);
    }

    [Fact]
    public void Calculate_ShouldClampToMinChance()
    {
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        SetupStat(target, StatType.Agility, 2000);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_ClampMin"
        };

        var chance = _service.CalculateHitChance(source, target, effect);

        chance.ShouldBe(MinChance);
    }

    [Fact]
    public void Calculate_ShouldClampToMaxChance()
    {
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        SetupStat(source, StatType.Attack, 2000);

        var effect = new EffectDefinition
        {
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            TargetRuleId = "T_ClampMax"
        };

        var chance = _service.CalculateHitChance(source, target, effect);

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
            RaceId = "RACE_TEST",
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