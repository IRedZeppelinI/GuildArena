using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
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

public class DamageResolutionServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly ILogger<DamageResolutionService> _loggerMock;
    private readonly DamageResolutionService _service;

    private readonly ModifierDefinition _fireBuff;
    private readonly ModifierDefinition _physicalResist;
    private readonly ModifierDefinition _genericBarrier;
    private readonly ModifierDefinition _fireBarrier;

    public DamageResolutionServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<DamageResolutionService>>();
        _service = new DamageResolutionService(_repoMock, _loggerMock);

        _fireBuff = new ModifierDefinition
        {
            Id = "BUFF_FIRE",
            Name = "Fire Up",
            Type = ModifierType.BLESS,
            DamageModifications = new() {
                new() { RequiredTag = "Fire", Type = ModificationType.FLAT, Value = 2 }
            }
        };

        _physicalResist = new ModifierDefinition
        {
            Id = "RESIST_PHYS",
            Name = "Iron Skin",
            Type = ModifierType.BLESS,
            DamageModifications = new() {
                // Agora usamos a Tag "Physical" que é adicionada automaticamente pelo DamageCategory.Physical
                // OU podemos manter "Martial" se o efeito tiver essa tag.
                // Aqui decidi usar "Physical" para testar a integração com a Categoria.
                new() { RequiredTag = "Physical", Type = ModificationType.PERCENTAGE, Value = -0.5f }
            }
        };

        _genericBarrier = new ModifierDefinition
        {
            Id = "BARRIER_GENERIC",
            Name = "Shield",
            Type = ModifierType.BLESS,
            Barrier = new BarrierProperties { BaseAmount = 100, BlockedTags = new() } // Bloqueia tudo
        };

        _fireBarrier = new ModifierDefinition
        {
            Id = "BARRIER_FIRE",
            Name = "Fire Ward",
            Type = ModifierType.BLESS,
            Barrier = new BarrierProperties { BaseAmount = 50, BlockedTags = new() { "Fire" } }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _fireBuff.Id, _fireBuff },
            { _physicalResist.Id, _physicalResist },
            { _genericBarrier.Id, _genericBarrier },
            { _fireBarrier.Id, _fireBarrier }
        });
    }

    [Fact]
    public void ResolveDamage_WithFlatBuff_ShouldIncreaseDamage()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Fire" },
            DamageCategory = DamageCategory.Magical, // Ex-Mystic
            TargetRuleId = "T1"
        };
        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "BUFF_FIRE" });

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };

        // Act
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(12f);
        result.AbsorbedDamage.ShouldBe(0);
    }

    [Fact]
    public void ResolveDamage_WithPercentageResist_ShouldReduceDamage()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.Physical, // Adiciona tag "Physical" automaticamente
            TargetRuleId = "T1"
        };
        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "RESIST_PHYS" });

        // Act
        var result = _service.ResolveDamage(20f, effect, source, target);

        // Assert
        // Resistência física (-50%) atua sobre 20 = 10
        result.FinalDamageToApply.ShouldBe(10f);
    }

    [Fact]
    public void ResolveDamage_WithBuffAndResist_ShouldCombineCorrectly()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Fire" },
            DamageCategory = DamageCategory.Physical, // Adiciona tag "Physical"
            TargetRuleId = "T1"
        };

        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "BUFF_FIRE" }); // +2 Flat

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "RESIST_PHYS" }); // -50% se for Physical

        // Act
        // Base: 10
        // Bonus Source: +2 (Flat) = 12
        // Resist Target: -50% (Percent) -> Multiplier 0.5
        // Final: 12 * 0.5 = 6
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(6f);
    }

    [Fact]
    public void ResolveDamage_WithGenericBarrier_ShouldAbsorbDamageAndUpdateState()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };
        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        var barrierMod = new ActiveModifier
        {
            DefinitionId = "BARRIER_GENERIC",
            CurrentBarrierValue = 20
        };
        target.ActiveModifiers.Add(barrierMod);

        // Act
        var result = _service.ResolveDamage(15f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(0f);
        result.AbsorbedDamage.ShouldBe(15f);
        result.IsFullyMitigated.ShouldBeTrue();

        barrierMod.CurrentBarrierValue.ShouldBe(5f);
    }

    [Fact]
    public void ResolveDamage_WithBarrier_OverflowDamage_ShouldPassThrough()
    {
        // Arrange
        var effect = new EffectDefinition { Tags = new(), DamageCategory = DamageCategory.Physical, TargetRuleId = "T1" };
        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        var barrierMod = new ActiveModifier { DefinitionId = "BARRIER_GENERIC", CurrentBarrierValue = 10 };
        target.ActiveModifiers.Add(barrierMod);

        // Act
        var result = _service.ResolveDamage(30f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(20f);
        result.AbsorbedDamage.ShouldBe(10f);
        barrierMod.CurrentBarrierValue.ShouldBe(0f);
    }

    [Fact]
    public void ResolveDamage_SpecificBarrier_ShouldIgnoreUnmatchedTags()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Ice" }, // Tag diferente de "Fire"
            DamageCategory = DamageCategory.Magical,
            TargetRuleId = "T1"
        };
        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        var barrierMod = new ActiveModifier { DefinitionId = "BARRIER_FIRE", CurrentBarrierValue = 50 };
        target.ActiveModifiers.Add(barrierMod);

        // Act
        var result = _service.ResolveDamage(20f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(20f); // Ignorou barreira
        result.AbsorbedDamage.ShouldBe(0f);
        barrierMod.CurrentBarrierValue.ShouldBe(50f);
    }

    [Fact]
    public void ResolveDamage_DamageCategoryAsTag_ShouldTriggerBarrier()
    {
        // Arrange
        // Barreira que bloqueia especificamente magia ("Magical")
        var mysticBarrierDef = new ModifierDefinition
        {
            Id = "BARRIER_MAGIC",
            Name = "Anti-Magic Shell",
            Type = ModifierType.BLESS,
            Barrier = new BarrierProperties { BaseAmount = 50, BlockedTags = new() { "Magical" } }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "BARRIER_MAGIC", mysticBarrierDef }
        });

        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.Magical, // Isto adiciona a tag "Magical" automaticamente
            TargetRuleId = "T1"
        };

        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };
        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };

        var barrierMod = new ActiveModifier { DefinitionId = "BARRIER_MAGIC", CurrentBarrierValue = 50 };
        target.ActiveModifiers.Add(barrierMod);

        // Act
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(0f);
        result.AbsorbedDamage.ShouldBe(10f);
    }

    [Fact]
    public void ResolveDamage_TrueDamage_ShouldIgnoreExistingResistances()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Physical" }, // Tem a tag física...
            DamageCategory = DamageCategory.True, // ...mas é True Damage
            TargetRuleId = "T1"
        };

        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "RESIST_PHYS" });

        // Act
        var result = _service.ResolveDamage(100f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(100f);
    }

    [Fact]
    public void ResolveDamage_TrueDamage_ShouldIgnoreBarriers()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.True,
            TargetRuleId = "T1"
        };

        var source = new Combatant { Id = 1, Name = "S", BaseStats = new() };

        var target = new Combatant { Id = 2, Name = "T", BaseStats = new() };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "BARRIER_GENERIC", CurrentBarrierValue = 100 });

        // Act
        var result = _service.ResolveDamage(50f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(50f);
        result.AbsorbedDamage.ShouldBe(0f);
        target.ActiveModifiers.First().CurrentBarrierValue.ShouldBe(100f);
    }
}