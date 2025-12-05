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
            CurrentLevel = 5,
            UnlockedPerkIds = new List<string>()
        };

        // 1. Raça (Dá bónus de HP e Ataque)
        var raceDef = new RaceDefinition
        {
            Id = "RACE_TEST",
            Name = "Orc",
            BonusStats = new BaseStats
            {
                Attack = 5,
                MaxHP = 50, // Bónus Racial de HP
                MaxActions = 0
            }
        };

        // 2. Herói (Stats Base e Crescimento)
        var charDef = new CharacterDefinition
        {
            Id = "HERO_TEST",
            Name = "Warrior",
            RaceId = "RACE_TEST",

            // Stats Base (Nível 1)
            BaseStats = new BaseStats
            {
                Attack = 10,
                Defense = 10,
                MaxHP = 100, // HP Base
                MaxActions = 1
            },

            // Crescimento por Nível
            StatsGrowthPerLevel = new BaseStats
            {
                Attack = 2,
                MaxHP = 10 // +10 HP por nível
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
        // Nível 5 significa 4 subidas de nível (5 - 1 = 4)

        // Attack: 10 (Base) + 5 (Raça) + (2 * 4) (Growth) = 23
        combatant.BaseStats.Attack.ShouldBe(23);

        // MaxHP: 100 (Base) + 50 (Raça) + (10 * 4) (Growth) = 190
        combatant.BaseStats.MaxHP.ShouldBe(190);

        // Verifica se as propriedades de atalho também ficaram corretas
        combatant.MaxHP.ShouldBe(190);
        combatant.CurrentHP.ShouldBe(190);

        // MaxActions: 1 (Base) + 0 (Raça) = 1
        combatant.BaseStats.MaxActions.ShouldBe(1);
    }

    [Fact]
    public void Create_ShouldApplyPassiveModifiers_FromRaceAndPerks()
    {
        // ARRANGE
        var hero = new HeroCharacter
        {
            Id = 1,
            CharacterDefinitionID = "HERO_A",
            CurrentLevel = 1,
            UnlockedPerkIds = new List<string> { "MOD_PERK" } // Perk do jogador
        };

        var raceDef = new RaceDefinition
        {
            Id = "RACE_A",
            Name = "Race A",
            RacialModifierIds = new List<string> { "MOD_RACIAL" } // Trait Racial
        };

        var charDef = new CharacterDefinition
        {
            Id = "HERO_A",
            RaceId = "RACE_A",
            Name = "A",
            BaseStats = new BaseStats(),
            StatsGrowthPerLevel = new BaseStats(),
            BasicAttackAbilityId = "ATK"
        };

        // Definições dos Modifiers
        var racialMod = new ModifierDefinition { Id = "MOD_RACIAL", Name = "Racial Trait" };
        var perkMod = new ModifierDefinition { Id = "MOD_PERK", Name = "Selected Perk" };

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
            { "MOD_PERK", perkMod }
        });

        // ACT
        var combatant = _factory.Create(hero, 1);

        // ASSERT
        combatant.ActiveModifiers.Count.ShouldBe(2);
        combatant.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_RACIAL");
        combatant.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_PERK");
    }

    [Fact]
    public void Create_ShouldLoadSkills_FromCharacterDefinition()
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
            SkillIds = new List<string> { "SKILL_1", "SKILL_2" }
        };

        var raceDef = new RaceDefinition { Id = "RACE_A", Name = "Race A" };

        _charRepo.TryGetDefinition("HERO_A", out Arg.Any<CharacterDefinition>()).
            Returns(x => { x[1] = charDef; return true; });

        _raceRepo.TryGetDefinition("RACE_A", out Arg.Any<RaceDefinition>()).
            Returns(x => { x[1] = raceDef; return true; });

        // Mock Abilities
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
}