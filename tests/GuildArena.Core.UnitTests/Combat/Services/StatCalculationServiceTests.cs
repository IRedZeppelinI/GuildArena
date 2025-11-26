using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories; 
using GuildArena.Domain.Definitions; 
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using NSubstitute; 
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class StatCalculationServiceTests
{
    private readonly IModifierDefinitionRepository _modifierDefinitionRepositoryMock; 
    private readonly StatCalculationService _statCalculationService;

    // Definições de Modifiers para os testes
    private readonly ModifierDefinition _attackBuff;
    private readonly ModifierDefinition _defenseBuff;

    public StatCalculationServiceTests()
    {
        // ARRANGE Global

        // Criar os mods para testes
        _attackBuff = new ModifierDefinition
        {
            Id = "MOD_ATTACK_UP",
            Name = "+10 Ataque",
            Type = ModifierType.BLESS,
            StatModifications = new() {
                new() { Stat = StatType.Attack, Type = ModificationType.FLAT, Value = 10 }
            }
        };
        _defenseBuff = new ModifierDefinition
        {
            Id = "MOD_DEFENSE_PERCENT",
            Name = "+20% Defesa",
            Type = ModifierType.BLESS,
            StatModifications = new() {
                new() { Stat = StatType.Defense, Type = ModificationType.PERCENTAGE, Value = 0.20f }
            }
        };

        // Criar o Mock do Repositório
        _modifierDefinitionRepositoryMock = Substitute.For<IModifierDefinitionRepository>();

        //  Configurar o Mock
        // _modifierDefinitionRepositoryMock devolve os mods definitions criados para testes
        _modifierDefinitionRepositoryMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _attackBuff.Id, _attackBuff },
            { _defenseBuff.Id, _defenseBuff }
        });

        // Injetar o Mock no Serviço
        _statCalculationService = new StatCalculationService(_modifierDefinitionRepositoryMock);
    }

    [Fact]
    public void GetStatValue_WithNoModifiers_ShouldReturnBaseStat()
    {
        //ARRANGE
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            BaseStats = new BaseStats { Attack = 10 }
        };

        // ACT
        var finalAttack = _statCalculationService.GetStatValue(combatant, StatType.Attack);

        // ASSERT
        finalAttack.ShouldBe(10);
    }

    [Fact]
    public void GetStatValue_WithOneFlatModifier_ShouldReturnBasePlusFlat()
    {
        // ARRANGE
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            BaseStats = new BaseStats { Attack = 10 }
        };

        // Aplicar o buff "+10 Attack"
        combatant.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_ATTACK_UP", 
            TurnsRemaining = 3,
            CasterId = 1
        });

        // ACT
        var finalAttack = _statCalculationService.GetStatValue(combatant, StatType.Attack);

        // ASSERT
        // (Base: 10 + Flat: 10) * (1 + 0%) = 20
        finalAttack.ShouldBe(20);
    }

    [Fact]
    public void GetStatValue_WithOnePercentageModifier_ShouldReturnBaseTimesPercent()
    {
        // ARRANGE
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Test",
            BaseStats = new BaseStats { Defense = 50 }
        };

        // Aplicar o buff "+20% Defesa"
        combatant.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_DEFENSE_PERCENT", // O mock do repo sabe o que é isto
            TurnsRemaining = 3,
            CasterId = 1
        });

        // ACT
        var finalDefense = _statCalculationService.GetStatValue(combatant, StatType.Defense);

        // 3. ASSERT
        // (Base: 50 + Flat: 0) * (1 + 0.20) = 60
        finalDefense.ShouldBe(60);
    }
}