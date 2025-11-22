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

public class EssenceServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly ILogger<EssenceService> _loggerMock;
    private readonly EssenceService _service;

    // Definitions for testing generation
    private readonly ModifierDefinition _manaSpringMod;
    private readonly ModifierDefinition _cursedDrainMod;
    private readonly ModifierDefinition _chaosGeneratorMod;

    public EssenceServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<EssenceService>>();
        _service = new EssenceService(_repoMock, _loggerMock);

        // 1. Fixed Generation (+1 Mind)
        _manaSpringMod = new ModifierDefinition
        {
            Id = "MOD_MANA_SPRING",
            Name = "Mana Spring",
            Type = ModifierType.BUFF,
            EssenceGenerationModifications = new() {
                new() { IsRandom = false, EssenceType = EssenceType.Mind, Amount = 1 }
            }
        };

        // 2. Random Drain (-1 Random)
        _cursedDrainMod = new ModifierDefinition
        {
            Id = "MOD_DRAIN",
            Name = "Leak",
            Type = ModifierType.DEBUFF,
            EssenceGenerationModifications = new() {
                new() { IsRandom = true, Amount = -1 }
            }
        };

        // 3. Random Generation (+2 Random)
        _chaosGeneratorMod = new ModifierDefinition
        {
            Id = "MOD_CHAOS",
            Name = "Chaos",
            Type = ModifierType.BUFF,
            EssenceGenerationModifications = new() {
                new() { IsRandom = true, Amount = 2 }
            }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _manaSpringMod.Id, _manaSpringMod },
            { _cursedDrainMod.Id, _cursedDrainMod },
            { _chaosGeneratorMod.Id, _chaosGeneratorMod }
        });
    }

    // --- TESTES DE GERAÇÃO ---

    [Fact]
    public void GenerateStartOfTurnEssence_TurnOne_ShouldGenerateTwoEssence()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };

        // Act
        _service.GenerateStartOfTurnEssence(player, 1);

        // Assert
        // Turno 1 = 2 Essence base
        player.EssencePool.Values.Sum().ShouldBe(2);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_TurnTwo_ShouldGenerateFourEssence()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };

        // Act
        _service.GenerateStartOfTurnEssence(player, 2);

        // Assert
        // Turno > 1 = 4 Essence base
        player.EssencePool.Values.Sum().ShouldBe(4);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_WithFixedModifier_ShouldAddSpecificType()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };
        player.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_MANA_SPRING" }); // +1 Mind

        // Act
        // Turno 2 (Gera 4 Random) + Mod (+1 Mind) = 5 Total
        _service.GenerateStartOfTurnEssence(player, 2);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(5);

        // Deve ter pelo menos 1 Mind (do modifier), mais o que calhar no random
        player.EssencePool.ContainsKey(EssenceType.Mind).ShouldBeTrue();
        player.EssencePool[EssenceType.Mind].ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_WithRandomDrain_ShouldReduceTotal()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };
        // Começa já com alguma essence para poder tirar
        player.EssencePool.Add(EssenceType.Vigor, 5);

        player.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_DRAIN" }); // -1 Random

        // Act
        // Turno 2 (Gera +4) - Drain (-1) = Net +3
        // Total esperado: 5 (Inicial) + 3 = 8
        _service.GenerateStartOfTurnEssence(player, 2);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(8);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_ShouldRespectCap()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 3 }; // Cap baixo
        player.EssencePool.Add(EssenceType.Vigor, 2); // Já tem 2

        // Act
        // Turno 2 tenta gerar +4. Total seria 6. Cap é 3.
        _service.GenerateStartOfTurnEssence(player, 2);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(3);
    }

    // --- TESTES DE VALIDAÇÃO (HasEnoughEssence) ---

    [Fact]
    public void HasEnoughEssence_WithExactChange_ShouldReturnTrue()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2);

        var costs = new List<EssenceCost> { new() { Type = EssenceType.Vigor, Amount = 2 } };

        // Act & Assert
        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MissingSpecificColor_ShouldReturnFalse()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 5); // Tem muito dinheiro, mas cor errada

        var costs = new List<EssenceCost> { new() { Type = EssenceType.Mind, Amount = 1 } };

        // Act & Assert
        _service.HasEnoughEssence(player, costs).ShouldBeFalse();
    }

    [Fact]
    public void HasEnoughEssence_NeutralCost_ShouldUseAnyColor()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 1);
        player.EssencePool.Add(EssenceType.Mind, 1);

        // Custo: 2 Neutral
        var costs = new List<EssenceCost> { new() { Type = EssenceType.Neutral, Amount = 2 } };

        // Act & Assert
        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MixedCost_Valid_ShouldReturnTrue()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2); // Paga o Vigor
        player.EssencePool.Add(EssenceType.Mind, 3);  // Sobra 3 para pagar o Neutral

        // Custo: 2 Vigor + 3 Neutral
        var costs = new List<EssenceCost>
        {
            new() { Type = EssenceType.Vigor, Amount = 2 },
            new() { Type = EssenceType.Neutral, Amount = 3 }
        };

        // Act & Assert
        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MixedCost_NotEnoughForNeutral_ShouldReturnFalse()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2); // Paga o Vigor exato
        player.EssencePool.Add(EssenceType.Mind, 1);  // Só tem 1 de sobra

        // Custo: 2 Vigor + 3 Neutral (Precisa de 3 de sobra, só tem 1)
        var costs = new List<EssenceCost>
        {
            new() { Type = EssenceType.Vigor, Amount = 2 },
            new() { Type = EssenceType.Neutral, Amount = 3 }
        };

        // Act & Assert
        _service.HasEnoughEssence(player, costs).ShouldBeFalse();
    }

    // --- TESTES DE PAGAMENTO (PayEssence) ---

    [Fact]
    public void PayEssence_ShouldDeductCorrectAmounts()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 5);
        player.EssencePool.Add(EssenceType.Mind, 5);

        var payment = new Dictionary<EssenceType, int>
        {
            { EssenceType.Vigor, 2 },
            { EssenceType.Mind, 3 }
        };

        // Act
        _service.ConsumeEssence(player, payment);

        // Assert
        player.EssencePool[EssenceType.Vigor].ShouldBe(3); // 5 - 2
        player.EssencePool[EssenceType.Mind].ShouldBe(2);  // 5 - 3
    }

    [Fact]
    public void PayEssence_ShouldNotGoBelowZero()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 1);

        var payment = new Dictionary<EssenceType, int>
        {
            { EssenceType.Vigor, 5 } // Tenta pagar mais do que tem
        };

        // Act
        _service.ConsumeEssence(player, payment);

        // Assert
        player.EssencePool[EssenceType.Vigor].ShouldBe(0); // Clamp a 0        
    }
}