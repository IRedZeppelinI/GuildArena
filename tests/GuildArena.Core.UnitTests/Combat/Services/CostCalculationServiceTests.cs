using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects.Targeting;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class CostCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly ILogger<CostCalculationService> _loggerMock;
    private readonly CostCalculationService _service;

    // Definições reutilizáveis
    private readonly ModifierDefinition _neutralWardMod;
    private readonly ModifierDefinition _valdrinHateMod; // Penalidade contra Valdrin
    private readonly ModifierDefinition _humanLoveMod;   // Desconto contra Humanos

    public CostCalculationServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<CostCalculationService>>();
        _service = new CostCalculationService(_repoMock, _loggerMock);

        // 1. Modifier: Ward (+1 Neutral Cost no targeting)
        _neutralWardMod = new ModifierDefinition
        {
            Id = "MOD_WARD_1",
            Name = "Magic Shell",
            Type = ModifierType.Bless,
            TargetingEssenceCosts = new() {
                new() { Type = EssenceType.Neutral, Amount = 1 }
            }
        };

        // 2. Modifier: Penalidade Racial (+1 Vigor se alvo for Valdrin)
        _valdrinHateMod = new ModifierDefinition
        {
            Id = "MOD_HATE_VALDRIN",
            Name = "Stone Breaker",
            Type = ModifierType.Bless, // É um buff no caster que custa mais, tecnicamente
            EssenceCostModifications = new() {
                new() {
                    TargetEssenceType = EssenceType.Vigor,
                    TargetRaceId = "RACE_VALDRIN",
                    Value = 1 // Penalidade
                }
            }
        };

        // 3. Modifier: Desconto Racial (-1 Mind se alvo for Humano)
        _humanLoveMod = new ModifierDefinition
        {
            Id = "MOD_LOVE_HUMAN",
            Name = "Humanity",
            Type = ModifierType.Bless,
            EssenceCostModifications = new() {
                new() {
                    TargetEssenceType = EssenceType.Mind,
                    TargetRaceId = "RACE_HUMAN",
                    Value = -1 // Desconto
                }
            }
        };

        // Configurar o Mock
        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _neutralWardMod.Id, _neutralWardMod },
            { _valdrinHateMod.Id, _valdrinHateMod },
            { _humanLoveMod.Id, _humanLoveMod }
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
        var target = CreateCombatant(2, 2, "RACE_HUMAN");

        // Input vazio (simulando ou manual ou auto, sem mods não interessa)
        var input = new AbilityTargets();

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, new List<Combatant> { target }, input);

        // Assert
        result.EssenceCosts.Count.ShouldBe(1);
        result.EssenceCosts.First(c => c.Type == EssenceType.Vigor).Amount.ShouldBe(2);
        result.HPCost.ShouldBe(10);
    }

    // --- TESTES DE WARD (MANUAL vs AUTO) ---

    [Fact]
    public void CalculateFinalCosts_Ward_WithManualTarget_ShouldApplyTax()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Zap",
            Costs = new() { new() { Type = EssenceType.Mind, Amount = 1 } }
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var target = CreateCombatant(2, 2, "RACE_HUMAN");
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_WARD_1" });

        // Simular Seleção Manual
        var input = new AbilityTargets 
        { 
            SelectedTargets = new() { { "Rule1", new List<int> { target.Id } } } 
        };

        // Act
        var result = _service.CalculateFinalCosts(
            caster,
            ability,
            new List<Combatant> { target },
            input);

        // Assert
        // 1 Mind (Base) + 1 Neutral (Taxa)
        result.EssenceCosts.Count.ShouldBe(2);
        result.EssenceCosts.ShouldContain(c => c.Type == EssenceType.Neutral && c.Amount == 1);
    }

    [Fact]
    public void CalculateFinalCosts_Ward_WithAutoTarget_ShouldIgnoreTax()
    {
        // Arrange
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Rain of Fire", // AoE ou Random
            Costs = new() { new() { Type = EssenceType.Mind, Amount = 1 } }
        };
        var caster = new CombatPlayer { PlayerId = 1 };

        var target = CreateCombatant(2, 2, "RACE_HUMAN");
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_WARD_1" });

        // Simular Seleção Automática (Input Vazio ou IDs que não correspondem a esta regra)
        var input = new AbilityTargets();

        // Act
        var result = _service.CalculateFinalCosts(
            caster,
            ability,
            new List<Combatant> { target },
            input);

        // Assert
        // Apenas 1 Mind (Base). A taxa foi ignorada.
        result.EssenceCosts.Count.ShouldBe(1);
        result.EssenceCosts.ShouldNotContain(c => c.Type == EssenceType.Neutral);
    }

    // --- TESTES DE CUSTOS RACIAIS ---

    [Fact]
    public void CalculateFinalCosts_RacialPenalty_ShouldApply_IfAnyManualTargetMatches()
    {
        // Arrange
        // Modificador: +1 Vigor se alvo for VALDRIN
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Attack",
            Costs = new() { new() { Type = EssenceType.Vigor, Amount = 1 } }
        };

        var caster = new CombatPlayer { PlayerId = 1 };
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HATE_VALDRIN" });

        var valdrinTarget = CreateCombatant(2, 2, "RACE_VALDRIN");
        var humanTarget = CreateCombatant(3, 2, "RACE_HUMAN");

        // Seleção Manual Mista (Basta um Valdrin para aplicar penalidade)
        var targets = new List<Combatant> { valdrinTarget, humanTarget };
        var input = new AbilityTargets
        {
            SelectedTargets = new() { { "Rule1", new List<int> { valdrinTarget.Id, humanTarget.Id } } }
        };

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, targets, input);

        // Assert
        // 1 Vigor (Base) + 1 Vigor (Penalidade) = 2 Vigor
        result.EssenceCosts.First(c => c.Type == EssenceType.Vigor).Amount.ShouldBe(2);
    }

    [Fact]
    public void CalculateFinalCosts_RacialPenalty_ShouldIgnore_IfNoTargetMatches()
    {
        // Arrange
        // Modificador: +1 Vigor se alvo for VALDRIN
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Attack",
            Costs = new() { new() { Type = EssenceType.Vigor, Amount = 1 } }
        };

        var caster = new CombatPlayer { PlayerId = 1 };
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_HATE_VALDRIN" });

        var humanTarget = CreateCombatant(2, 2, "RACE_HUMAN"); // Não é Valdrin

        var targets = new List<Combatant> { humanTarget };
        var input = new AbilityTargets
        {
            SelectedTargets = new() { { "Rule1", new List<int> { humanTarget.Id } } }
        };

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, targets, input);

        // Assert
        // Apenas 1 Vigor (Base). Penalidade ignorada.
        result.EssenceCosts.First(c => c.Type == EssenceType.Vigor).Amount.ShouldBe(1);
    }

    [Fact]
    public void CalculateFinalCosts_RacialDiscount_ShouldApply_OnlyIfAllManualTargetsMatch()
    {
        // Arrange
        // Modificador: -1 Mind se alvo for HUMAN
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Inspire",
            Costs = new() { new() { Type = EssenceType.Mind, Amount = 2 } }
        };

        var caster = new CombatPlayer { PlayerId = 1 };
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_LOVE_HUMAN" });

        var human1 = CreateCombatant(2, 1, "RACE_HUMAN");
        var human2 = CreateCombatant(3, 1, "RACE_HUMAN");

        // Todos os alvos manuais são Humanos -> Desconto aplica-se
        var targets = new List<Combatant> { human1, human2 };
        var input = new AbilityTargets
        {
            SelectedTargets = new() { { "Rule1", new List<int> { human1.Id, human2.Id } } }
        };

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, targets, input);

        // Assert
        // 2 Mind (Base) - 1 Mind (Desconto) = 1 Mind
        result.EssenceCosts.First(c => c.Type == EssenceType.Mind).Amount.ShouldBe(1);
    }

    [Fact]
    public void CalculateFinalCosts_RacialDiscount_ShouldFail_IfOneTargetIsInvalid()
    {
        // Arrange
        // Modificador: -1 Mind se alvo for HUMAN
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Inspire Mixed",
            Costs = new() { new() { Type = EssenceType.Mind, Amount = 2 } }
        };

        var caster = new CombatPlayer { PlayerId = 1 };
        caster.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_LOVE_HUMAN" });

        var human = CreateCombatant(2, 1, "RACE_HUMAN");
        var valdrin = CreateCombatant(3, 1, "RACE_VALDRIN"); // Intruso!

        // Lista mista -> Desconto falha (regra ALL)
        var targets = new List<Combatant> { human, valdrin };
        var input = new AbilityTargets
        {
            SelectedTargets = new() { { "Rule1", new List<int> { human.Id, valdrin.Id } } }
        };

        // Act
        var result = _service.CalculateFinalCosts(caster, ability, targets, input);

        // Assert
        // 2 Mind (Base). Desconto não aplicado.
        result.EssenceCosts.First(c => c.Type == EssenceType.Mind).Amount.ShouldBe(2);
    }

    // --- Helpers ---

    private Combatant CreateCombatant(int id, int ownerId, string raceId)
    {
        return new Combatant
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"C_{id}",
            RaceId = raceId,
            BaseStats = new BaseStats(),
            CurrentHP = 100,
            MaxHP = 100
        };
    }
}