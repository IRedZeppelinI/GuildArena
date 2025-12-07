using GuildArena.Core.Combat.Factories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Factories;

public class CombatantFactoryTests
{
    private readonly ICharacterDefinitionRepository _charRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly ILogger<CombatantFactory> _logger;
    private readonly CombatantFactory _factory;

    public CombatantFactoryTests()
    {
        _charRepo = Substitute.For<ICharacterDefinitionRepository>();
        _raceRepo = Substitute.For<IRaceDefinitionRepository>();
        _abilityRepo = Substitute.For<IAbilityDefinitionRepository>();
        _modifierRepo = Substitute.For<IModifierDefinitionRepository>();
        _logger = Substitute.For<ILogger<CombatantFactory>>();

        _factory = new CombatantFactory(
            _charRepo, _raceRepo, _abilityRepo, _modifierRepo, _logger);
    }

    [Fact]
    public void Create_ShouldCalculateStatsAndHP_UsingUnifiedBaseStats()
    {
        // ARRANGE
        var hero = new HeroCharacter
        {
            Id = 100,
            CharacterDefinitionID = "HERO_TEST",
            CurrentLevel = 5            
        };

        // 1. Raça
        var raceDef = new RaceDefinition
        {
            Id = "RACE_TEST",
            Name = "Orc",
            BonusStats = new BaseStats
            {
                Attack = 5,
                MaxHP = 50,
                MaxActions = 0
            }
        };

        // 2. Herói
        var charDef = new CharacterDefinition
        {
            Id = "HERO_TEST",
            Name = "Warrior",
            RaceId = "RACE_TEST",
            BaseStats = new BaseStats
            {
                Attack = 10,
                Defense = 10,
                MaxHP = 100,
                MaxActions = 1
            },
            StatsGrowthPerLevel = new BaseStats
            {
                Attack = 2,
                MaxHP = 10
            },
            BasicAttackAbilityId = "ATK_BASIC"
        };

        // Mocks
        _charRepo.TryGetDefinition(hero.CharacterDefinitionID, out Arg.Any<CharacterDefinition>())
            .Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition(charDef.RaceId, out Arg.Any<RaceDefinition>())
            .Returns(x => { x[1] = raceDef; return true; });

        _abilityRepo.TryGetDefinition(Arg.Any<string>(), out Arg.Any<AbilityDefinition>())
            .Returns(true);

        // ACT
        var combatant = _factory.Create(hero, ownerId: 1);

        // ASSERT
        combatant.BaseStats.Attack.ShouldBe(23); // 10 + 5 + (2*4)
        combatant.BaseStats.MaxHP.ShouldBe(190); // 100 + 50 + (10*4)
        combatant.MaxHP.ShouldBe(190);
        combatant.CurrentHP.ShouldBe(190);
        combatant.BaseStats.MaxActions.ShouldBe(1);
    }

    [Fact]
    public void Create_ShouldApplyModifiers_From_Race_Trait_And_Loadout()
    {
        // ARRANGE
        var hero = new HeroCharacter
        {
            Id = 1,
            CharacterDefinitionID = "HERO_A",
            CurrentLevel = 1            
        };

        // Loadout selecionado no lobby
        var playerLoadout = new List<string> { "MOD_LOADOUT_RUNES" };

        var raceDef = new RaceDefinition
        {
            Id = "RACE_A",
            Name = "Race A",
            RacialModifierIds = new List<string> { "MOD_RACIAL" } // 1. RACIAL
        };

        var charDef = new CharacterDefinition
        {
            Id = "HERO_A",
            RaceId = "RACE_A",
            Name = "A",
            TraitModifierId = "MOD_HERO_TRAIT", // 2. TRAIT FIXO
            BaseStats = new BaseStats(),
            StatsGrowthPerLevel = new BaseStats(),
            BasicAttackAbilityId = "ATK"
        };

        // Definições dos Modifiers 
        var racialMod = new ModifierDefinition { Id = "MOD_RACIAL", Name = "Racial Trait" };
        var traitMod = new ModifierDefinition { Id = "MOD_HERO_TRAIT", Name = "Hero Identity" };
        var loadoutMod = new ModifierDefinition { Id = "MOD_LOADOUT_RUNES", Name = "Selected Runes" };

        _charRepo.TryGetDefinition("HERO_A", out Arg.Any<CharacterDefinition>()).
            Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition("RACE_A", out Arg.Any<RaceDefinition>()).
            Returns(x => { x[1] = raceDef; return true; });

        _abilityRepo.TryGetDefinition(Arg.Any<string>(), out Arg.Any<AbilityDefinition>()).
            Returns(true);

        // Mock do Repositório de Modifiers
        _modifierRepo.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { "MOD_RACIAL", racialMod },
            { "MOD_HERO_TRAIT", traitMod },
            { "MOD_LOADOUT_RUNES", loadoutMod }
        });

        // ACT - Passamos o loadout como argumento extra
        var combatant = _factory.Create(hero, 1, playerLoadout);

        // ASSERT
        combatant.ActiveModifiers.Count.ShouldBe(3);
        combatant.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_RACIAL");
        combatant.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_HERO_TRAIT");
        combatant.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_LOADOUT_RUNES");
    }

    [Fact]
    public void Create_ShouldLoadAbilities_FromCharacterDefinition()
    {
        // ARRANGE
        var hero = new HeroCharacter { Id = 1, CharacterDefinitionID = "HERO_A", CurrentLevel = 1 };

        var charDef = new CharacterDefinition
        {
            Id = "HERO_A",
            RaceId = "RACE_A",
            Name = "A",
            BaseStats = new BaseStats(),
            StatsGrowthPerLevel = new BaseStats(),
            BasicAttackAbilityId = "ATK_BASIC",
            AbilityIds = new List<string> { "SKILL_1", "SKILL_2" }
        };

        var raceDef = new RaceDefinition { Id = "RACE_A", Name = "Race A" };

        _charRepo.TryGetDefinition("HERO_A", out Arg.Any<CharacterDefinition>()).
            Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition("RACE_A", out Arg.Any<RaceDefinition>()).
            Returns(x => { x[1] = raceDef; return true; });

        // Mocks Abilities
        _abilityRepo.TryGetDefinition("ATK_BASIC", out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = new AbilityDefinition { Id = "ATK_BASIC", Name = "Basic" }; return true; });

        _abilityRepo.TryGetDefinition("SKILL_1", out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = new AbilityDefinition { Id = "SKILL_1", Name = "S1" }; return true; });

        _abilityRepo.TryGetDefinition("SKILL_2", out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = new AbilityDefinition { Id = "SKILL_2", Name = "S2" }; return true; });

        // ACT
        var combatant = _factory.Create(hero, 1);

        // ASSERT
        combatant.BasicAttack.ShouldNotBeNull();
        combatant.BasicAttack.Id.ShouldBe("ATK_BASIC");

        combatant.Abilities.Count.ShouldBe(2);
        combatant.Abilities.ShouldContain(a => a.Id == "SKILL_1");
        combatant.Abilities.ShouldContain(a => a.Id == "SKILL_2");
    }

    [Fact]
    public void Create_ShouldResolveSpecialAbility_FromGuardId()
    {
        // ARRANGE
        var hero = new HeroCharacter { Id = 1, CharacterDefinitionID = "HERO_GUARD", CurrentLevel = 1 };

        var charDef = new CharacterDefinition
        {
            Id = "HERO_GUARD",
            RaceId = "RACE_A",
            Name = "Tank",
            BaseStats = new BaseStats(),
            StatsGrowthPerLevel = new BaseStats(),
            BasicAttackAbilityId = "ATK",
            // Configurar apenas o Guard
            GuardAbilityId = "ABIL_GUARD_01",
            FocusAbilityId = null
        };

        var raceDef = new RaceDefinition { Id = "RACE_A", Name = "Race" };

        _charRepo.TryGetDefinition("HERO_GUARD", out Arg.Any<CharacterDefinition>())
            .Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition("RACE_A", out Arg.Any<RaceDefinition>())
            .Returns(x => { x[1] = raceDef; return true; });

        _abilityRepo.TryGetDefinition(Arg.Any<string>(), out Arg.Any<AbilityDefinition>())
            .Returns(true); // Default mock for basic attack

        // Mock específico para o Guard
        _abilityRepo.TryGetDefinition("ABIL_GUARD_01", out Arg.Any<AbilityDefinition>())
            .Returns(x => {
                x[1] = new AbilityDefinition { Id = "ABIL_GUARD_01", Name = "Iron Skin" };
                return true;
            });

        // ACT
        var combatant = _factory.Create(hero, 1);

        // ASSERT
        combatant.SpecialAbility.ShouldNotBeNull();
        combatant.SpecialAbility.Id.ShouldBe("ABIL_GUARD_01");
        combatant.SpecialAbility.Name.ShouldBe("Iron Skin");
    }

    [Fact]
    public void Create_ShouldResolveSpecialAbility_FromFocusId()
    {
        // ARRANGE
        var hero = new HeroCharacter { Id = 1, CharacterDefinitionID = "HERO_FOCUS", CurrentLevel = 1 };

        var charDef = new CharacterDefinition
        {
            Id = "HERO_FOCUS",
            RaceId = "RACE_A",
            Name = "Mage",
            BaseStats = new BaseStats(),
            StatsGrowthPerLevel = new BaseStats(),
            BasicAttackAbilityId = "ATK",
            // Configurar apenas o Focus
            GuardAbilityId = null,
            FocusAbilityId = "ABIL_FOCUS_01"
        };

        var raceDef = new RaceDefinition { Id = "RACE_A", Name = "Race" };

        _charRepo.TryGetDefinition("HERO_FOCUS", out Arg.Any<CharacterDefinition>())
            .Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition("RACE_A", out Arg.Any<RaceDefinition>())
            .Returns(x => { x[1] = raceDef; return true; });

        _abilityRepo.TryGetDefinition(Arg.Any<string>(), out Arg.Any<AbilityDefinition>())
            .Returns(true);

        // Mock específico para o Focus
        _abilityRepo.TryGetDefinition("ABIL_FOCUS_01", out Arg.Any<AbilityDefinition>())
            .Returns(x => {
                x[1] = new AbilityDefinition { Id = "ABIL_FOCUS_01", Name = "Meditate" };
                return true;
            });

        // ACT
        var combatant = _factory.Create(hero, 1);

        // ASSERT
        combatant.SpecialAbility.ShouldNotBeNull();
        combatant.SpecialAbility.Id.ShouldBe("ABIL_FOCUS_01");
        combatant.SpecialAbility.Name.ShouldBe("Meditate");
    }
}