using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class CostCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly CostCalculationService _service;

    // Definições reutilizáveis
    private readonly ModifierDefinition _fireDiscountMod;
    private readonly ModifierDefinition _neutralWardMod;
    private readonly ModifierDefinition _bloodMagicMod;
    private readonly ModifierDefinition _bloodWardMod;

    public CostCalculationServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _service = new CostCalculationService(_repoMock);

        // 1. Modifier: -1 Vigor (Fire) Cost
        _fireDiscountMod = new ModifierDefinition
        {
            Id = "MOD_VIGOR_DISCOUNT",
            Name = "Fire Mastery",
            Type = ModifierType.BUFF,
            EssenceCostModifications = new() {
                new() { TargetEssenceType = EssenceType.Vigor, Value = -1 }
            }
        };

        // 2. Modifier: Ward (+1 Neutral Cost no targeting)
        _neutralWardMod = new ModifierDefinition
        {
            Id = "MOD_WARD_1",
            Name = "Magic Shell",
            Type = ModifierType.BUFF,
            TargetingEssenceCosts = new() {
                new() { Type = EssenceType.Neutral, Amount = 1 }
            }
        };

        // 3. Modifier: HP Cost Increase (+5 HP Cost)
        _bloodMagicMod = new ModifierDefinition
        {
            Id = "MOD_BLOOD_MAGIC",
            Name = "Blood Sacrifice",
            Type = ModifierType.DEBUFF,
            HPCostModifications = new() {
                new() { Value = 5 } // Increases HP cost by 5
            }
        };

        // 4. Modifier: HP Ward (+10 HP Cost to target)
        _bloodWardMod = new ModifierDefinition
        {
            Id = "MOD_BLOOD_WARD",
            Name = "Thorns",
            Type = ModifierType.BUFF,
            TargetingHPCost = 10
        };

        // Configurar o Mock para devolver estes modifiers
        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _fireDiscountMod.Id, _fireDiscountMod },
            { _neutralWardMod.Id, _neutralWardMod },
            { _bloodMagicMod.Id, _bloodMagicMod },
            { _bloodWardMod.Id, _bloodWardMod }
        });
    }

    [Fact]
    public void CalculateFinalCosts_NoModifiers_ShouldReturnBaseCosts()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Fireball",
            Costs = new() { new() { Type = EssenceType.Vigor, Amount = 2 } },
            HPCost = 10
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var target = new Combatant
        {
            Id = 2,
            OwnerId = 2,
            Name = "Dummy Target",
            BaseStats = new BaseStats()
        };

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant> { target });

        // Assert
        result.EssenceCosts.Count.ShouldBe(1);
        result.EssenceCosts.First(c => c.Type == EssenceType.Vigor).Amount.ShouldBe(2);
        result.HPCost.ShouldBe(10);
    }

    [Fact]
    public void CalculateFinalCosts_WithDiscountModifier_ShouldReduceEssenceCost()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Fireball",
            Costs = new() { new() { Type = EssenceType.Vigor, Amount = 2 } }
        };
        var caster = new CombatPlayer { PlayerId = 1 };
        // Adiciona o buff de desconto ao caster
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_VIGOR_DISCOUNT" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant>());

        // Assert
        // 2 Vigor - 1 Desconto = 1 Vigor
        result.EssenceCosts.First(c => c.Type == EssenceType.Vigor).Amount.ShouldBe(1);
    }

    [Fact]
    public void CalculateFinalCosts_WithWardOnTarget_ShouldAddTaxToCost()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Zap",
            Costs = new() { new() { Type = EssenceType.Mind, Amount = 1 } }
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var target = new Combatant
        {
            Id = 2,
            OwnerId = 2,
            Name = "Warded Enemy",
            BaseStats = new BaseStats()
        };
        // O alvo tem Ward (+1 Neutral)
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_WARD_1" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant> { target });

        // Assert
        // Total: 1 Mind (Base) + 1 Neutral (Ward)
        result.EssenceCosts.Count.ShouldBe(2);
        result.EssenceCosts.ShouldContain(c => c.Type == EssenceType.Mind && c.Amount == 1);
        result.EssenceCosts.ShouldContain(c => c.Type == EssenceType.Neutral && c.Amount == 1);
    }

    [Fact]
    public void CalculateFinalCosts_WithSelfTargeting_ShouldIgnoreWard()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Self Heal",
            Costs = new() { new() { Type = EssenceType.Light, Amount = 1 } }
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        // O alvo o próprio caster  (OwnerId = 1)
        var target = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Self",
            BaseStats = new BaseStats()
        };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_WARD_1" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant> { target });

        // Assert
        // Ward deve ser ignorado porque caster é o alvo
        result.EssenceCosts.Count.ShouldBe(1);
        result.EssenceCosts.First().Type.ShouldBe(EssenceType.Light);
        result.EssenceCosts.First().Amount.ShouldBe(1);
    }

    [Fact]
    public void CalculateFinalCosts_HPCostModifiers_ShouldAdjustTotalHPCost()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Blood Strike",
            HPCost = 10,
            Costs = new() // Sem custo de essence
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        // Caster tem "Blood Magic" (+5 HP Cost)
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_BLOOD_MAGIC" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant>());

        // Assert
        // 10 Base + 5 Penalidade = 15
        result.HPCost.ShouldBe(15);
    }

    [Fact]
    public void CalculateFinalCosts_HPWardOnTarget_ShouldAddHPTax()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Poke",
            HPCost = 0,
            Costs = new()
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var target = new Combatant
        {
            Id = 2,
            OwnerId = 2,
            Name = "Blood Warded Enemy",
            BaseStats = new BaseStats()
        };
        // Alvo tem "Blood Ward" (+10 HP Cost para ser alvejado)
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_BLOOD_WARD" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant> { target });

        // Assert
        // 0 Base + 10 Ward = 10 HP
        result.HPCost.ShouldBe(10);
    }

    [Fact]
    public void CalculateFinalCosts_ShouldNeverReturnNegativeCosts()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Cheap Spell",
            Costs = new() { new() { Type = EssenceType.Vigor, Amount = 1 } },
            HPCost = 0
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var superDiscount = new ModifierDefinition
        {
            Id = "SUPER_DISCOUNT",
            Name = "Free",
            Type = ModifierType.BUFF,
            EssenceCostModifications = new() { new() { TargetEssenceType = EssenceType.Vigor, Value = -5 } },
            HPCostModifications = new() { new() { Value = -10 } }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> {
            { superDiscount.Id, superDiscount }
        });

        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "SUPER_DISCOUNT" });

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant>());

        // Assert
        result.EssenceCosts.ShouldBeEmpty(); // Custo desceu a 0 ou menos, deve desaparecer
        result.HPCost.ShouldBe(0); // Nunca negativo
    }
}