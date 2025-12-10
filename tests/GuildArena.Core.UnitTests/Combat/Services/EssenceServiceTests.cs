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

public class EssenceServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly ILogger<EssenceService> _loggerMock;
    private readonly IRandomProvider _randomMock;
    private readonly EssenceService _service;

    // Definitions for testing generation
    private readonly ModifierDefinition _manaSpringMod;
    private readonly ModifierDefinition _cursedDrainMod;
    private readonly ModifierDefinition _chaosGeneratorMod;

    public EssenceServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<EssenceService>>();
        _randomMock = Substitute.For<IRandomProvider>();

        // Mock Random: Devolve sempre 0 para determinismo nos testes
        _randomMock.Next(Arg.Any<int>()).Returns(0);

        _service = new EssenceService(_repoMock, _loggerMock, _randomMock);

        // 1. Fixed Generation (+1 Mind)
        _manaSpringMod = new ModifierDefinition
        {
            Id = "MOD_MANA_SPRING",
            Name = "Mana Spring",
            Type = ModifierType.Bless,
            EssenceGenerationModifications = new() {
                new() { IsRandom = false, EssenceType = EssenceType.Mind, Amount = 1 }
            }
        };

        // 2. Random Drain (-1 Random)
        _cursedDrainMod = new ModifierDefinition
        {
            Id = "MOD_DRAIN",
            Name = "Leak",
            Type = ModifierType.Curse,
            EssenceGenerationModifications = new() {
                new() { IsRandom = true, Amount = -1 }
            }
        };

        // 3. Random Generation (+2 Random)
        _chaosGeneratorMod = new ModifierDefinition
        {
            Id = "MOD_CHAOS",
            Name = "Chaos",
            Type = ModifierType.Bless,
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

    // --- TESTES DE GERAÇÃO  ---

    [Fact]
    public void GenerateStartOfTurnEssence_WithBase2_ShouldGenerateTwoEssence()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };

        // Act - Simular o "Turno 1 do Player Inicial" passando explicitamente 2
        _service.GenerateStartOfTurnEssence(player, baseAmount: 2);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(2);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_Default_ShouldGenerateFourEssence()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };

        // Act - Simulamos um turno normal (default 4)
        _service.GenerateStartOfTurnEssence(player);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(4);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_WithFixedModifier_ShouldAddSpecificType()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };
        player.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_MANA_SPRING" }); // +1 Mind

        // Act (Base 4 + 1 Mod = 5)
        _service.GenerateStartOfTurnEssence(player, baseAmount: 4);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(5);
        player.EssencePool.ContainsKey(EssenceType.Mind).ShouldBeTrue();
        player.EssencePool[EssenceType.Mind].ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_WithRandomDrain_ShouldReduceTotal()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 10 };
        player.EssencePool.Add(EssenceType.Vigor, 5); // Começa com 5
        player.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_DRAIN" }); // -1 Random

        // Act
        // Base 2 (ex: turno 1) - 1 Drain = Net +1.
        // Total esperado: 5 (Inicial) + 1 = 6.
        _service.GenerateStartOfTurnEssence(player, baseAmount: 2);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(6);
    }

    [Fact]
    public void GenerateStartOfTurnEssence_ShouldRespectCap()
    {
        // Arrange
        var player = new CombatPlayer { PlayerId = 1, MaxTotalEssence = 3 }; // Cap baixo
        player.EssencePool.Add(EssenceType.Vigor, 2);

        // Act (Tenta gerar 4, mas só cabe 1)
        _service.GenerateStartOfTurnEssence(player, baseAmount: 4);

        // Assert
        player.EssencePool.Values.Sum().ShouldBe(3);
    }

    // --- TESTES DE VALIDAÇÃO (HasEnoughEssence) ---        

    [Fact]
    public void HasEnoughEssence_WithExactChange_ShouldReturnTrue()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2);
        var costs = new List<EssenceAmount> { new() { Type = EssenceType.Vigor, Amount = 2 } };

        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MissingSpecificColor_ShouldReturnFalse()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 5);
        var costs = new List<EssenceAmount> { new() { Type = EssenceType.Mind, Amount = 1 } };

        _service.HasEnoughEssence(player, costs).ShouldBeFalse();
    }

    [Fact]
    public void HasEnoughEssence_NeutralCost_ShouldUseAnyColor()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 1);
        player.EssencePool.Add(EssenceType.Mind, 1);
        var costs = new List<EssenceAmount> { new() { Type = EssenceType.Neutral, Amount = 2 } };

        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MixedCost_Valid_ShouldReturnTrue()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2);
        player.EssencePool.Add(EssenceType.Mind, 3);
        var costs = new List<EssenceAmount>
        {
            new() { Type = EssenceType.Vigor, Amount = 2 },
            new() { Type = EssenceType.Neutral, Amount = 3 }
        };

        _service.HasEnoughEssence(player, costs).ShouldBeTrue();
    }

    [Fact]
    public void HasEnoughEssence_MixedCost_NotEnoughForNeutral_ShouldReturnFalse()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 2);
        player.EssencePool.Add(EssenceType.Mind, 1);
        var costs = new List<EssenceAmount>
        {
            new() { Type = EssenceType.Vigor, Amount = 2 },
            new() { Type = EssenceType.Neutral, Amount = 3 }
        };

        _service.HasEnoughEssence(player, costs).ShouldBeFalse();
    }

    // --- TESTES DE CONSUMO ESSENCE ---

    [Fact]
    public void ConsumeEssence_ShouldDeductCorrectAmounts()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 5);
        player.EssencePool.Add(EssenceType.Mind, 5);

        var payment = new Dictionary<EssenceType, int>
        {
            { EssenceType.Vigor, 2 },
            { EssenceType.Mind, 3 }
        };

        _service.ConsumeEssence(player, payment);

        player.EssencePool[EssenceType.Vigor].ShouldBe(3);
        player.EssencePool[EssenceType.Mind].ShouldBe(2);
    }

    [Fact]
    public void ConsumeEssence_ShouldNotGoBelowZero()
    {
        var player = new CombatPlayer { PlayerId = 1 };
        player.EssencePool.Add(EssenceType.Vigor, 1);

        var payment = new Dictionary<EssenceType, int>
        {
            { EssenceType.Vigor, 5 }
        };

        _service.ConsumeEssence(player, payment);

        player.EssencePool[EssenceType.Vigor].ShouldBe(0);
    }
}