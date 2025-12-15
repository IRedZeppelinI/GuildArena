using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.State;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class StatCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _modifierRepoMock;
    private readonly StatCalculationService _service;

    private readonly ModifierDefinition _attackBuff;
    private readonly ModifierDefinition _defenseBuff;
    private readonly ModifierDefinition _healthBuff;

    public StatCalculationServiceTests()
    {
        _attackBuff = new ModifierDefinition
        {
            Id = "MOD_ATTACK_UP",
            Name = "+10 Ataque",
            Type = ModifierType.Bless,
            StatModifications = new() {
                new() { Stat = StatType.Attack, Type = ModificationType.FLAT, Value = 10 }
            }
        };
        _defenseBuff = new ModifierDefinition
        {
            Id = "MOD_DEFENSE_PERCENT",
            Name = "+20% Defesa",
            Type = ModifierType.Bless,
            StatModifications = new() {
                new() { Stat = StatType.Defense, Type = ModificationType.PERCENTAGE, Value = 0.20f }
            }
        };
        _healthBuff = new ModifierDefinition
        {
            Id = "MOD_HP_BOOST",
            Name = "+50 MaxHP",
            Type = ModifierType.Bless,
            StatModifications = new() {
                new() { Stat = StatType.MaxHP, Type = ModificationType.FLAT, Value = 50 }
            }
        };

        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>();
        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _attackBuff.Id, _attackBuff },
            { _defenseBuff.Id, _defenseBuff },
            { _healthBuff.Id, _healthBuff }
        });

        _service = new StatCalculationService(_modifierRepoMock);
    }

    [Fact]
    public void GetStatValue_WithNoModifiers_ShouldReturnBaseStat()
    {
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats { Attack = 10 }
        };

        var result = _service.GetStatValue(combatant, StatType.Attack);

        result.ShouldBe(10);
    }

    [Fact]
    public void GetStatValue_WithOneFlatModifier_ShouldReturnBasePlusFlat()
    {
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats { Attack = 10 }
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_ATTACK_UP" });

        var result = _service.GetStatValue(combatant, StatType.Attack);

        // 10 + 10 = 20
        result.ShouldBe(20);
    }

    [Fact]
    public void GetStatValue_WithOnePercentageModifier_ShouldReturnBaseTimesPercent()
    {
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats { Defense = 50 }
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_DEFENSE_PERCENT" });

        var result = _service.GetStatValue(combatant, StatType.Defense);

        // 50 * 1.20 = 60
        result.ShouldBe(60);
    }

    [Fact]
    public void GetStatValue_MaxHP_ShouldIncludeModifiers()
    {
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Tank",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats { MaxHP = 100 }
        };

        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HP_BOOST" }); // +50 Flat

        var result = _service.GetStatValue(combatant, StatType.MaxHP);

        // 100 Base + 50 Modifier = 150
        result.ShouldBe(150);
    }

    [Fact]
    public void GetStatValue_MaxHP_ShouldNeverBeLessThanOne()
    {
        var curseMod = new ModifierDefinition
        {
            Id = "MOD_CURSE",
            Name = "Curse",
            Type = ModifierType.Curse,
            StatModifications = new() { new()
            {
                Stat = StatType.MaxHP,
                Type = ModificationType.FLAT,
                Value = -200
            } }
        };

        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "MOD_CURSE", curseMod }
        });

        var localService = new StatCalculationService(_modifierRepoMock);

        var combatant = new Combatant
        {
            Id = 1,
            Name = "Victim",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats { MaxHP = 100 }
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_CURSE" });

        var result = localService.GetStatValue(combatant, StatType.MaxHP);

        // Deve ser clampado a 1, não 0 ou negativo
        result.ShouldBe(1);
    }
}