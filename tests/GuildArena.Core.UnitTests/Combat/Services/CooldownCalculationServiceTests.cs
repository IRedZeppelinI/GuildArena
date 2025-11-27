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

namespace GuildArena.Core.UnitTests.Combat.Services;

public class CooldownCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _modRepoMock;
    private readonly ILogger<CooldownCalculationService> _loggerMock;
    private readonly ICooldownCalculationService _service;

    // Definições de Modifiers para os testes
    private readonly ModifierDefinition _cdHasteBuff;
    private readonly ModifierDefinition _cdFrenzyBuff;
    private readonly ModifierDefinition _cdNatureBuff;

    public CooldownCalculationServiceTests()
    {
        // ARRANGE Global
        _loggerMock = Substitute.For<ILogger<CooldownCalculationService>>();
        _modRepoMock = Substitute.For<IModifierDefinitionRepository>();

        // Criar os mods para testes
        _cdHasteBuff = new ModifierDefinition
        {
            Id = "MOD_HASTE",
            Name = "-1 CD",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { Type = ModificationType.FLAT, Value = -1 } // -1 a todas as habilidades
            }
        };

        _cdFrenzyBuff = new ModifierDefinition
        {
            Id = "MOD_FRENZY",
            Name = "-50% CD",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { Type = ModificationType.PERCENTAGE, Value = -0.5f } // -50% a todas as habilidades
            }
        };

        _cdNatureBuff = new ModifierDefinition
        {
            Id = "MOD_NATURE_CD",
            Name = "-2 CD Nature",
            Type = ModifierType.Bless,
            CooldownModifications = new() {
                new() { RequiredTag = "Nature", Type = ModificationType.FLAT, Value = -2 } // -2 só a "Nature"
            }
        };

        // Configurar o Mock do Repositório
        _modRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _cdHasteBuff.Id, _cdHasteBuff },
            { _cdFrenzyBuff.Id, _cdFrenzyBuff },
            { _cdNatureBuff.Id, _cdNatureBuff }
        });

        // Injetar os Mocks no Serviço (SUT)
        _service = new CooldownCalculationService(_modRepoMock, _loggerMock);
    }

    [Fact]
    public void GetFinalCooldown_WithNoModifiers_ShouldReturnBaseCooldown()
    {
        // Arrange       
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 5 };
        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };

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
        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" }); // -1 Flat

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        // 5 + (-1) = 4
        finalCd.ShouldBe(4);
    }

    [Fact]
    public void GetFinalCooldown_WithPercentageModifier_ShouldReturnBaseTimesPercent()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 10 };
        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_FRENZY" }); // -50% Percent

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        // (10 + 0) * (1 - 0.5) = 5
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

        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_NATURE_CD" }); // -2 "Nature"

        // Act
        var natureCd = _service.GetFinalCooldown(combatant, abilityNature);
        var fireCd = _service.GetFinalCooldown(combatant, abilityFire);

        // Assert
        natureCd.ShouldBe(3); // 5 - 2 = 3
        fireCd.ShouldBe(5);   // Sem "Nature" tag, sem mudança
    }

    [Fact]
    public void GetFinalCooldown_WithMultipleModifiers_ShouldStackCorrectly()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 10 };
        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" });  // -1 Flat
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_FRENZY" }); // -50% Percent

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        // Fórmula: (Base + Flat) * (1 + Percent)
        // (10 + (-1)) * (1 + (-0.5)) = 9 * 0.5 = 4.5
        // Round(4.5) = 5
        finalCd.ShouldBe(5);
    }

    [Fact]
    public void GetFinalCooldown_ShouldNeverReturnNegative()
    {
        // Arrange        
        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability", BaseCooldown = 1 };
        var combatant = new Combatant { Id = 1, Name = "Test", BaseStats = new BaseStats() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HASTE" }); // -1 Flat

        // Act
        var finalCd = _service.GetFinalCooldown(combatant, ability);

        // Assert
        // (1 + (-1)) * (1 + 0) = 0
        finalCd.ShouldBe(0);
    }
}