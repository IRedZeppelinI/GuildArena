using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Modifiers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class DamageResolutionServiceTests
{
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly ILogger<DamageResolutionService> _loggerMock;
    private readonly DamageResolutionService _service;

    private readonly ModifierDefinition _fireBuff;
    private readonly ModifierDefinition _physicalResist;
    private readonly ModifierDefinition _slayerMod;
    private readonly ModifierDefinition _orcShieldMod;
    private readonly ModifierDefinition _genericBarrier;
    private readonly ModifierDefinition _fireBarrier;
    private readonly ModifierDefinition _mysticBarrierDef;

    public DamageResolutionServiceTests()
    {
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<DamageResolutionService>>();
        _service = new DamageResolutionService(_repoMock, _loggerMock);

        _fireBuff = new ModifierDefinition
        {
            Id = "BUFF_FIRE",
            Name = "Fire Up",
            Type = ModifierType.Bless,
            DamageModifications = new()
            {
                new() { RequiredTag = "Fire", Type = ModificationType.FLAT, Value = 2 }
            }
        };

        _physicalResist = new ModifierDefinition
        {
            Id = "RESIST_PHYS",
            Name = "Iron Skin",
            Type = ModifierType.Bless,
            DamageModifications = new()
            {
                new() { RequiredTag = "Physical", Type = ModificationType.PERCENTAGE, Value = -0.5f }
            }
        };

        _slayerMod = new ModifierDefinition
        {
            Id = "MOD_SLAYER",
            Name = "Valdrin Slayer",
            Type = ModifierType.Bless,
            DamageModifications = new()
            {
                new()
                {
                    RequiredTag = "Melee",
                    TargetRaceId = "RACE_VALDRIN",
                    Type = ModificationType.FLAT,
                    Value = 10
                }
            }
        };

        _orcShieldMod = new ModifierDefinition
        {
            Id = "MOD_ORC_SHIELD",
            Name = "Hates Humans",
            Type = ModifierType.Bless,
            DamageModifications = new()
            {
                new()
                {
                    RequiredTag = "Melee",
                    TargetRaceId = "RACE_HUMAN",
                    Type = ModificationType.FLAT,
                    Value = -5
                }
            }
        };

        _genericBarrier = new ModifierDefinition
        {
            Id = "BARRIER_GENERIC",
            Name = "Shield",
            Type = ModifierType.Bless,
            Barrier = new BarrierProperties { BaseAmount = 100, BlockedTags = new() }
        };

        _fireBarrier = new ModifierDefinition
        {
            Id = "BARRIER_FIRE",
            Name = "Fire Ward",
            Type = ModifierType.Bless,
            Barrier = new BarrierProperties { BaseAmount = 50, BlockedTags = new() { "Fire" } }
        };

        _mysticBarrierDef = new ModifierDefinition
        {
            Id = "BARRIER_MAGIC",
            Name = "Anti-Magic Shell",
            Type = ModifierType.Bless,
            Barrier = new BarrierProperties { BaseAmount = 50, BlockedTags = new() { "Magical" } }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { _fireBuff.Id, _fireBuff },
            { _physicalResist.Id, _physicalResist },
            { _slayerMod.Id, _slayerMod },
            { _orcShieldMod.Id, _orcShieldMod },
            { _genericBarrier.Id, _genericBarrier },
            { _fireBarrier.Id, _fireBarrier },
            { _mysticBarrierDef.Id, _mysticBarrierDef }
        });
    }

    [Fact]
    public void ResolveDamage_WithFlatBuff_ShouldIncreaseDamage()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Fire" },
            DamageCategory = DamageCategory.Magical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "BUFF_FIRE" });

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };

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
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "RESIST_PHYS" });

        // Act
        var result = _service.ResolveDamage(20f, effect, source, target);

        // Assert
        // 20 * 0.5 = 10
        result.FinalDamageToApply.ShouldBe(10f);
    }

    [Fact]
    public void ResolveDamage_WithBuffAndResist_ShouldCombineCorrectly()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Fire" },
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "BUFF_FIRE" }); // +2 Flat

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
        target.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "RESIST_PHYS" }); // -50%

        // Act
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

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
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
        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
        var barrierMod = new ActiveModifier
        {
            DefinitionId = "BARRIER_GENERIC",
            CurrentBarrierValue = 10
        };
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
            Tags = new() { "Ice" },
            DamageCategory = DamageCategory.Magical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
        var barrierMod = new ActiveModifier
        {
            DefinitionId = "BARRIER_FIRE",
            CurrentBarrierValue = 50
        };
        target.ActiveModifiers.Add(barrierMod);

        // Act
        var result = _service.ResolveDamage(20f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(20f);
        result.AbsorbedDamage.ShouldBe(0f);
    }

    [Fact]
    public void ResolveDamage_DamageCategoryAsTag_ShouldTriggerBarrier()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new(),
            DamageCategory = DamageCategory.Magical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };

        var barrierMod = new ActiveModifier
        {
            DefinitionId = "BARRIER_MAGIC",
            CurrentBarrierValue = 50
        };
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
            Tags = new() { "Physical" },
            DamageCategory = DamageCategory.True,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
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

        var source = new Combatant
        {
            Id = 1,
            Name = "S",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "T",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new()
        };
        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "BARRIER_GENERIC",
            CurrentBarrierValue = 100
        });

        // Act
        var result = _service.ResolveDamage(50f, effect, source, target);

        // Assert
        result.FinalDamageToApply.ShouldBe(50f);
        result.AbsorbedDamage.ShouldBe(0f);
    }

    // --- TESTES DE RAÇA NOVOS ---

    [Fact]
    public void ResolveDamage_WithRacialSlayer_ShouldIncreaseDamage_IfRaceMatches()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Melee" },
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Hero",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            BaseStats = new()
        };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_SLAYER" }); // +10 vs Valdrin

        var target = new Combatant
        {
            Id = 2,
            Name = "Enemy",
            RaceId = "RACE_VALDRIN",
            CurrentHP = 100,
            BaseStats = new()
        };

        // Act
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        // 10 Base + 10 Slayer = 20
        result.FinalDamageToApply.ShouldBe(20f);
    }

    [Fact]
    public void ResolveDamage_WithRacialSlayer_ShouldIgnore_IfRaceMismatch()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Melee" },
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Hero",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            BaseStats = new()
        };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_SLAYER" });

        var target = new Combatant
        {
            Id = 2,
            Name = "Enemy",
            RaceId = "RACE_HUMAN", // Não é Valdrin
            CurrentHP = 100,
            BaseStats = new()
        };

        // Act
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        // Bónus ignorado
        result.FinalDamageToApply.ShouldBe(10f);
    }

    [Fact]
    public void ResolveDamage_WithRacialResist_ShouldReduceDamage_IfAttackerRaceMatches()
    {
        // Arrange
        var effect = new EffectDefinition
        {
            Tags = new() { "Melee" },
            DamageCategory = DamageCategory.Physical,
            TargetRuleId = "T1"
        };

        var source = new Combatant
        {
            Id = 1,
            Name = "Human Attacker",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            BaseStats = new()
        };

        var target = new Combatant
        {
            Id = 2,
            Name = "Orc Defender",
            RaceId = "RACE_ORC",
            CurrentHP = 100,
            BaseStats = new()
        };
        target.ActiveModifiers.Add(new ActiveModifier 
        { 
            DefinitionId = "MOD_ORC_SHIELD" 
        }); // -5 se atacante for Humano

        // Act
        var result = _service.ResolveDamage(10f, effect, source, target);

        // Assert
        // 10 - 5 = 5
        result.FinalDamageToApply.ShouldBe(5f);
    }
}