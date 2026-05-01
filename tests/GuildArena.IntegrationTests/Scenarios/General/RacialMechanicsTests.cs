using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
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
            OwnerId = 2, // Inimigo (Cadeira 2)
            Name = "Valdrin Unit",
            RaceId = "RACE_VALDRIN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 0 },
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_RACIAL_VALDRIN_SKIN", TurnsRemaining = -1 }
            }
        };

        // Atacante
        var attacker = new Combatant
        {
            Id = 101,
            OwnerId = 1, // Jogador 1
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
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, UserId = "user-123" }, 
                new() { PlayerId = 2, UserId = "enemy-456" }
            }
        };
        await stateRepo.SaveAsync(combatId, gameState);

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
            OwnerId = 2,
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
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, UserId = "user-123", EssencePool = new() }, 
                new() { PlayerId = 2, UserId = "enemy-456", EssencePool = new() }
            }
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

        var totalEssence = player.EssencePool.Values.Sum();

        totalEssence.ShouldBe
            (2,
            "Deep Focus should generate exactly 2 units of essence (Random types).");
    }

    [Fact]
    public void Kymera_FluxConductor_ShouldGenerateFlux_OnCombatStart()
    {
        // ARRANGE
        var combatId = Guid.NewGuid().ToString();

        var kymeraUnit = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Kymera Test Subject",
            RaceId = "RACE_KYMERA",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats(),
            Position = 1,
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_RACIAL_KYMERA_TRAIT", TurnsRemaining = -1 }
            }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { kymeraUnit },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, UserId = "user-123", EssencePool = new() } 
            }
        };

        var triggerProcessor = GetService<ITriggerProcessor>();
        var combatEngine = GetService<ICombatEngine>();

        // ACT
        var context = new TriggerContext
        {
            Source = kymeraUnit,
            Target = kymeraUnit,
            GameState = gameState,
            Tags = new HashSet<string> { "StartCombat" }
        };

        triggerProcessor.ProcessTriggers(Domain.Enums.Modifiers.ModifierTrigger.ON_COMBAT_START, context);
        combatEngine.ProcessPendingActions(gameState);

        // ASSERT
        var player = gameState.Players.First(p => p.PlayerId == 1);

        player.EssencePool.TryGetValue(Domain.Enums.Resources.EssenceType.Flux, out int fluxAmount).ShouldBeTrue();
        fluxAmount.ShouldBe(1, "Kymera should start with 1 Flux Essence.");

        kymeraUnit.ActiveModifiers.ShouldBeEmpty("Racial modifier should be removed after triggering.");
    }
}