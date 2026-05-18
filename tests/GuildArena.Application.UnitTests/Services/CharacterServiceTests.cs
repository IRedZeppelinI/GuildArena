using GuildArena.Application.Abstractions;
using GuildArena.Application.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Shared.DTOs.Combat;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Services;

public class CharacterServiceTests
{
    private readonly ICharacterDefinitionRepository _characterRepoMock;
    private readonly IRaceDefinitionRepository _raceRepoMock;
    private readonly IAbilityDefinitionRepository _abilityRepoMock;
    private readonly IModifierDefinitionRepository _modifierRepoMock;
    private readonly IEffectTooltipService _tooltipServiceMock;
    private readonly CharacterService _service;

    public CharacterServiceTests()
    {
        _characterRepoMock = Substitute.For<ICharacterDefinitionRepository>();
        _raceRepoMock = Substitute.For<IRaceDefinitionRepository>();
        _abilityRepoMock = Substitute.For<IAbilityDefinitionRepository>();
        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>();
        _tooltipServiceMock = Substitute.For<IEffectTooltipService>();

        _service = new CharacterService(
            _characterRepoMock, _raceRepoMock, _abilityRepoMock, _modifierRepoMock, _tooltipServiceMock);
    }

    [Fact]
    public void GetCharacterDetails_ShouldExtractTraitsAndMapAbilities_WhenCharacterExists()
    {
        // ARRANGE
        var defId = "HERO_TEST";

        var charDef = new CharacterDefinition
        {
            Id = defId,
            Name = "Hero",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { MaxHP = 100 },
            StatsGrowthPerLevel = new BaseStats(),
            TraitModifierId = "MOD_HERO_TRAIT",
            AbilityIds = new List<string> { "ABIL_1" }
        };

        var raceDef = new RaceDefinition
        {
            Id = "RACE_HUMAN",
            Name = "Human",
            Description = "Human Lore",
            RacialModifierIds = new List<string> { "MOD_RACE_TRAIT" },
            BonusStats = new BaseStats { Attack = 5, MaxActions = 0 }
        };

        var heroTrait = new ModifierDefinition { Id = "MOD_HERO_TRAIT", Name = "Hero Trait", Description = "Hero Desc" };
        var raceTrait = new ModifierDefinition { Id = "MOD_RACE_TRAIT", Name = "Race Trait", Description = "Race Desc" };

        var abilityDef = new AbilityDefinition
        {
            Id = "ABIL_1",
            Name = "Strike",
            Effects = new List<EffectDefinition> { new EffectDefinition { TargetRuleId = "T1" } }
        };

        _characterRepoMock.TryGetDefinition(defId, out Arg.Any<CharacterDefinition>())
            .Returns(x => { x[1] = charDef; return true; });

        _raceRepoMock.TryGetDefinition("RACE_HUMAN", out Arg.Any<RaceDefinition>())
            .Returns(x => { x[1] = raceDef; return true; });

        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "MOD_HERO_TRAIT", heroTrait },
            { "MOD_RACE_TRAIT", raceTrait }
        });

        _abilityRepoMock.TryGetDefinition("ABIL_1", out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = abilityDef; return true; });

        _tooltipServiceMock.GeneratePreview(Arg.Any<Combatant>(), Arg.Any<EffectDefinition>())
            .Returns(new AbilityEffectSummaryDto { PredictedValue = 50 });

        // ACT
        var result = _service.GetCharacterDetails(defId);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        var dto = result.Value;
        dto.Name.ShouldBe("Hero");
        dto.Attack.ShouldBe(5); // Recebeu o bónus da raça através do Fantasma

        // Verifica os Traits segundo a nova lógica (Fundidos no Lineage)
        dto.Traits.Count.ShouldBe(2); // Lineage (Lore + Stats + Racial Mods) + Hero Trait

        var lineageTrait = dto.Traits.Single(t => t.Name == "Lineage" && t.IsRacial);
        lineageTrait.DescriptionLines.ShouldContain("Human Lore");
        lineageTrait.DescriptionLines.ShouldContain("Racial Stats: +5 Attack");
        lineageTrait.DescriptionLines.ShouldContain("• Race Trait: Race Desc"); 

        var heroSpecificTrait = dto.Traits.Single(t => t.Name == "Hero Trait" && !t.IsRacial);
        heroSpecificTrait.DescriptionLines.ShouldContain("Hero Desc");

        // Verifica as Habilidades mapeadas
        dto.Abilities.Count.ShouldBe(1);
        dto.Abilities[0].Effects.Count.ShouldBe(1);
        dto.Abilities[0].Effects[0].PredictedValue.ShouldBe(50);
    }
}