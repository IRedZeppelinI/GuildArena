using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class CooldownCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _modRepoMock;
    private readonly ILogger<CooldownCalculationService> _loggerMock;
    private readonly ICooldownCalculationService _service;

    private readonly ModifierDefinition _cdHasteBuff;
    private readonly ModifierDefinition _cdFrenzyBuff;
    private readonly ModifierDefinition _cdNatureBuff;

    public CooldownCalculationServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<CooldownCalculationService>>();
        _modRepoMock = Substitute.For<IModifierDefinitionRepository>();

        _cdHasteBuff = new ModifierDefinition
        {
            Id = "MOD_HASTE",
            Name = "-1 CD",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { Type = ModificationType.FLAT, Value = -1 }
            }
        };

        _cdFrenzyBuff = new ModifierDefinition
        {
            Id = "MOD_FRENZY",
            Name = "-50% CD",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { Type = ModificationType.PERCENTAGE, Value = -0.5f }
            }
        };

        _cdNatureBuff = new ModifierDefinition
        {
            Id = "MOD_NATURE_CD",
            Name = "-2 CD Nature",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { RequiredTag = "Nature", Type = ModificationType.FLAT, Value = -2 }
            }
        };

        _modRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _cdHasteBuff.Id, _cdHasteBuff },
            { _cdFrenzyBuff.Id, _cdFrenzyBuff },
            { _cdNatureBuff.Id, _cdNatureBuff }
        });

        _service = new CooldownCalculationService(_modRepoMock, _loggerMock);
    }

    [Fact]
    public void GetFinalCooldown_WithNoModifiers_ShouldReturnBaseCooldown()
    {
        // Arrange       
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 5 };
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        finalCd.ShouldBe(5);
    }

    [Fact]
    public void GetFinalCooldown_WithFlatModifier_ShouldReturnBaseMinusFlat()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 5 };
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" });

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        finalCd.ShouldBe(4);
    }

    [Fact]
    public void GetFinalCooldown_WithPercentageModifier_ShouldReturnBaseTimesPercent()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 10 };
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_FRENZY" });

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        finalCd.ShouldBe(5);
    }

    [Fact]
    public void GetFinalCooldown_WithTagModifier_ShouldOnlyApplyToTaggedAbility()
    {
        // Arrange        
        var abilityNature = new AbilityDefinition
        {
            Id = "A_NATURE",
            Name = "Nature Test",
            BaseCooldown = 5,
            Tags = new() { "Nature" }
        };

        var abilityFire = new AbilityDefinition
        {
            Id = "A_FIRE",
            Name = "Fire Test",
            BaseCooldown = 5,
            Tags = new() { "Fire" }
        };

        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_NATURE_CD" });

        // Act
        var natureCd = _service.GetFinalCooldown(combatant, abilityNature);
        var fireCd = _service.GetFinalCooldown(combatant, abilityFire);

        // Assert
        natureCd.ShouldBe(3);
        fireCd.ShouldBe(5);
    }

    [Fact]
    public void GetFinalCooldown_WithMultipleModifiers_ShouldStackCorrectly()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 10 };
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" });
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_FRENZY" });

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        finalCd.ShouldBe(5);
    }

    [Fact]
    public void GetFinalCooldown_ShouldNeverReturnNegative()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 1 };
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" });

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        finalCd.ShouldBe(0);
    }
}