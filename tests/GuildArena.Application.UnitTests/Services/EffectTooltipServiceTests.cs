using GuildArena.Application.Services;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Modifiers;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Services;

public class EffectTooltipServiceTests
{
    private readonly IStatCalculationService _statServiceMock;
    private readonly IModifierDefinitionRepository _modifierRepoMock;
    private readonly EffectTooltipService _service;

    public EffectTooltipServiceTests()
    {
        _statServiceMock = Substitute.For<IStatCalculationService>();
        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>();

        _service = new EffectTooltipService(_statServiceMock, _modifierRepoMock);
    }

    [Fact]
    public void GeneratePreview_DamageEffect_ShouldCalculateRawMath()
    {
        // ARRANGE
        var source = new Combatant { Id = 1, Name = "Attacker", RaceId = "RACE_HUMAN", BaseStats = new BaseStats() };

        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            BaseAmount = 15,
            ScalingStat = StatType.Attack,
            ScalingFactor = 1.2f,
            TargetRuleId = "T1"
        };

        _statServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);

        // ACT
        var result = _service.GeneratePreview(source, effectDef);

        // ASSERT
        // Math: 15 + (10 * 1.2) = 27
        result.PredictedValue.ShouldBe(27);
        result.Type.ShouldBe(EffectType.DAMAGE);
        result.ModifierName.ShouldBeNull();
    }

    [Fact]
    public void GeneratePreview_DamageEffect_ShouldIncludeGeneralDamageModifiers()
    {
        // ARRANGE
        var source = new Combatant { Id = 1, Name = "Attacker", RaceId = "RACE_HUMAN", BaseStats = new BaseStats() };
        source.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_SLAYER" });

        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            DamageCategory = DamageCategory.Physical,
            BaseAmount = 10,
            ScalingStat = StatType.Attack,
            ScalingFactor = 1.0f,
            Tags = new List<string> { "Physical" },
            TargetRuleId = "T1"
        };

        _statServiceMock.GetStatValue(source, StatType.Attack).Returns(10f); // Base Math = 20

        var slayerDef = new ModifierDefinition
        {
            Id = "MOD_SLAYER",
            Name = "Slayer",
            DamageModifications = new List<DamageModification>
            {
                new DamageModification { RequiredTag = "Physical", Type = ModificationType.FLAT, Value = 5 } // +5 Damage
            }
        };

        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "MOD_SLAYER", slayerDef }
        });

        // ACT
        var result = _service.GeneratePreview(source, effectDef);

        // ASSERT
        // Math: 10 + (10 * 1.0) = 20. Mod: +5. Total: 25.
        result.PredictedValue.ShouldBe(25);
    }

    [Fact]
    public void GeneratePreview_ApplyModifierEffect_ShouldFetchModifierText()
    {
        // ARRANGE
        var source = new Combatant { Id = 1, Name = "Buffer", RaceId = "RACE_HUMAN", BaseStats = new BaseStats() };

        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = "MOD_GUARD",
            TargetRuleId = "T1"
        };

        var guardDef = new ModifierDefinition
        {
            Id = "MOD_GUARD",
            Name = "Guarding",
            Description = "Raises defense."
        };

        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "MOD_GUARD", guardDef }
        });

        // ACT
        var result = _service.GeneratePreview(source, effectDef);

        // ASSERT
        result.Type.ShouldBe(EffectType.APPLY_MODIFIER);
        result.ModifierName.ShouldBe("Guarding");
        result.ModifierDescription.ShouldBe("Raises defense.");
        result.PredictedValue.ShouldBe(0);
    }
}