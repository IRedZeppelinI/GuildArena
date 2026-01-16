using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.General;

public class RacialMechanicsTests : IntegrationTestBase
{
    [Fact]
    public async Task Valdrin_RockSkin_ShouldReducePhysicalDamage_FlatAmount()
    {
        // ARRANGE
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // Defensor Valdrin com o Modifier Racial
        var valdrin = new Combatant
        {
            Id = -1,
            OwnerId = 0,
            Name = "Valdrin Unit",
            RaceId = "RACE_VALDRIN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 0 }, // Defesa 0 para isolar o teste do modifier
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_RACIAL_VALDRIN_SKIN", TurnsRemaining = -1 }
            }
        };

        // Atacante
        var attacker = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Attacker",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { Attack = 10 },
            Position = 1,
            CurrentHP = 100,
            MaxHP = 100,
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_COMMON_SLASH", Name = "Slash" }
            }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { attacker, valdrin },
            Players = new List<CombatPlayer> { new() { PlayerId = 1 } }
        };
        await stateRepo.SaveAsync(combatId, gameState);

        // Habilidade "Slash": Base 5 + Attack 10 = 15 Dano Físico.
        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_COMMON_SLASH",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new()
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await stateRepo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Cálculo: 15 (Raw) - 3 (Rock Skin Flat) = 12 Dano.
        target.CurrentHP.ShouldBe(88); // 100 - 12
    }

    [Fact]
    public async Task Psylian_DeepFocus_ShouldGenerateDoubleEssence()
    {
        // ARRANGE
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        var psylianUnit = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Psylian Recruit",
            RaceId = "RACE_PSYLIAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats(),
            Position = 1,
            SpecialAbility = new AbilityDefinition { Id = "ABIL_PSYLIAN_FOCUS", Name = "Deep Focus" }
        };

        var dummyTarget = new Combatant
        {
            Id = -1,
            OwnerId = 0,
            Name = "Dummy",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { psylianUnit, dummyTarget },
            // Começa com 0 Mana para garantir que o saldo final vem apenas da habilidade
            Players = new List<CombatPlayer> { new() { PlayerId = 1, EssencePool = new() } }
        };

        await stateRepo.SaveAsync(combatId, gameState);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_PSYLIAN_FOCUS",
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new()
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await stateRepo.GetAsync(combatId);
        var player = state!.Players.First(p => p.PlayerId == 1);

        // Como a habilidade gera "2 Random Essence", não podemos prever a cor exata 
        // (depende do RNG), mas sabemos que a soma total tem de ser 2.
        var totalEssence = player.EssencePool.Values.Sum();

        totalEssence.ShouldBe
            (2,
            "Deep Focus should generate exactly 2 units of essence (Random types).");
    }

    /* 
     * Nota sobre Humanos:
     * O bónus de +1 AP dos humanos é aplicado na criação (Factory) através dos BaseStats.
     * Esse teste já existe no CombatantFactoryTests.cs.
     */


}