using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.ValueObjects.Modifiers;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class HitChanceServiceTests
{
    private readonly IStatCalculationService _statServiceMock;
    private readonly IModifierDefinitionRepository _modifierRepoMock;
    private readonly HitChanceService _service;

    // Constantes copiadas do serviço para validação matemática
    private const float BaseChance = 1.0f;
    private const float OffenseFactor = 0.005f;
    private const float DefenseFactor = 0.01f;
    private const float LevelDeltaFactor = 0.02f;
    private const float MinChance = 0.05f;
    private const float MaxChance = 1.0f;

    public HitChanceServiceTests()
    {
        _statServiceMock = Substitute.For<IStatCalculationService>();
        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>();

        // --- CORREÇÃO DA DÚVIDA DO MOCK ---
        // Garante que o repo devolve sempre um dicionário vazio por defeito.
        // Assim, os testes antigos não rebentam com NullReferenceException.
        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>());

        _service = new HitChanceService(_statServiceMock, _modifierRepoMock);
    }

    [Fact]
    public void Calculate_CanBeEvadedFalse_ShouldAlwaysReturnOne()
    {
        var effect = new EffectDefinition
        {
            TargetRuleId = "T_ANY", // Required
            CanBeEvaded = false
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
            TargetRuleId = "T_MELEE", // Required
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee
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
            TargetRuleId = "T_ANY", // Required
            CanBeEvaded = true,
            Delivery = delivery
        };

        _service.CalculateHitChance(source, target, effect);

        _statServiceMock.Received(1).GetStatValue(source, expectedOffenseStat);
        _statServiceMock.Received(1).GetStatValue(target, expectedDefenseStat);
    }

    [Fact]
    public void Calculate_ShouldClampToMinChance()
    {
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        SetupStat(target, StatType.Agility, 2000); // Agilidade absurda

        var effect = new EffectDefinition
        {
            TargetRuleId = "T_CLAMP", // Required
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee
        };

        var chance = _service.CalculateHitChance(source, target, effect);

        chance.ShouldBe(MinChance);
    }


    [Fact]
    public void Calculate_WithBlindModifier_ShouldReduceHitChance()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_BLIND" });

        var target = CreateCombatant(2, 1);

        var blindDef = new ModifierDefinition
        {
            Id = "MOD_BLIND",
            Name = "Blind",
            HitChanceModifications = new() { new() { Value = -0.20f } }
        };
        _modifierRepoMock.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { "MOD_BLIND", blindDef } });

        var effect = new EffectDefinition
        {
            TargetRuleId = "T_TEST",
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee
        };

        // CORREÇÃO: Stats a 0 para garantir que a BaseChance é exatamente 1.0 (100%)
        // Assim isolamos o teste do modificador.
        SetupStat(source, StatType.Attack, 0);
        SetupStat(target, StatType.Agility, 0);

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // 1.0 (Base) - 0.20 (Blind) = 0.80
        chance.ShouldBe(0.80f, 0.01f);
    }

    [Fact]
    public void Calculate_WithBlurModifier_ShouldReduceHitChance_ViaEvasion()
    {
        // ARRANGE
        var source = CreateCombatant(1, 1);
        var target = CreateCombatant(2, 1);
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_BLUR" });

        var blurDef = new ModifierDefinition
        {
            Id = "MOD_BLUR",
            Name = "Blur",
            EvasionModifications = new() { new() { Value = 0.15f, RequiredDamageCategory = "Physical" } }
        };
        _modifierRepoMock.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { "MOD_BLUR", blurDef } });

        var effect = new EffectDefinition
        {
            TargetRuleId = "T_TEST",
            CanBeEvaded = true,
            Delivery = DeliveryMethod.Melee,
            DamageCategory = DamageCategory.Physical
        };

        // CORREÇÃO: Stats a 0
        SetupStat(source, StatType.Attack, 0);
        SetupStat(target, StatType.Agility, 0);

        // ACT
        var chance = _service.CalculateHitChance(source, target, effect);

        // ASSERT
        // 1.0 (Base) - 0.15 (Evasion) = 0.85
        chance.ShouldBe(0.85f, 0.01f);
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