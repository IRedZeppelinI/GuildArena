using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using NSubstitute;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.Heroes;

public class VexTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupVexCombat(
        Dictionary<EssenceType, int> essence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // Vex (Player 1)
        var vex = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Vex",
            RaceId = "RACE_KYMERA",
            CurrentHP = 140,
            MaxHP = 140,
            // Attack 14 -> Thorns = 2 + (14 * 0.25) = 5.5 (Round -> 5)
            BaseStats = new BaseStats { Attack = 14, Defense = 6, Magic = 4 },
            Position = 1,
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_VEX_TRAIT", TurnsRemaining = -1, CasterId = 101 }
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_VEX_RECKLESS", Name = "Reckless" },
                new() { Id = "ABIL_VEX_CHAOS", Name = "Chaos" },
                new() { Id = "ABIL_VEX_FUEL", Name = "Fuel" },
                new() { Id = "ABIL_VEX_ULTI", Name = "Ulti" }
            }
        };

        // Inimigo Melee (Player 2)
        var enemy = new Combatant
        {
            Id = 201,
            OwnerId = 2,
            Name = "Enemy Striker",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            // DEFESA 0 para garantir que o Thorns causa dano visível no teste
            BaseStats = new BaseStats { Attack = 10, Defense = 0 },
            Position = 1,
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_COMMON_SLASH", Name = "Slash", Tags = new List<string> { "Melee", "Physical" } }
            }
        };

        // Inimigo Secundário
        var enemy2 = new Combatant
        {
            Id = 202,
            OwnerId = 2,
            Name = "Enemy Dummy",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { MagicDefense = 0 },
            Position = 2
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { vex, enemy, enemy2 },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, EssencePool = essence },
                new() { PlayerId = 2, EssencePool = new() }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task Trait_SpikedCarapace_ShouldReflectDamage_OnMeleeHit()
    {
        // ARRANGE: Preparar turno do inimigo
        var (combatId, mediator, repo) = await SetupVexCombat(new());
        var state = await repo.GetAsync(combatId);

        state!.CurrentPlayerId = 2; // Vez do Inimigo
        var userMock = GetService<ICurrentUserService>();
        userMock.UserId.Returns(2); // Autenticar como Inimigo
        await repo.SaveAsync(combatId, state);

        // Inimigo ataca Vex com Melee
        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 201,
            AbilityId = "ABIL_COMMON_SLASH",
            TargetSelections = new() { { "TGT", new List<int> { 101 } } }, // Alvo Vex
            Payment = new()
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var newState = await repo.GetAsync(combatId);
        var vex = newState!.Combatants.First(c => c.Id == 101);
        var enemy = newState.Combatants.First(c => c.Id == 201);

        // 1. Vex levou dano (Slash)
        vex.CurrentHP.ShouldBeLessThan(140);

        // 2. Enemy levou dano refletido (Thorns)
        // Cálculo: 2 (Base) + (14 Attack * 0.25) = 5.5 -> 5 Dano.
        // Como Defesa é 0, perde exatamente 5 HP.
        enemy.CurrentHP.ShouldBe(95, "Thorns trait did not reflect the expected damage.");
    }

    [Fact]
    public async Task RecklessStrike_ShouldCostHP_AndDealHeavyDamage()
    {
        var (combatId, mediator, repo) = await SetupVexCombat(new());

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101, // Vex
            AbilityId = "ABIL_VEX_RECKLESS",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new()
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var vex = state!.Combatants.First(c => c.Id == 101);
        var enemy = state.Combatants.First(c => c.Id == 201);

        // 1. Vex pagou custo de HP
        vex.CurrentHP.ShouldBe(130); // 140 - 10

        // 2. Inimigo levou dano
        // 14 * 1.4 = 19.6 -> 19 Dano. (Defesa 0)
        enemy.CurrentHP.ShouldBe(81);
    }

    [Fact]
    public async Task ChaosBarrage_ShouldCostFlux_AndHitTwoTargets()
    {
        var (combatId, mediator, repo) = await SetupVexCombat(new() { { EssenceType.Flux, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_VEX_CHAOS",
            TargetSelections = new(), // Random
            Payment = new() { { EssenceType.Flux, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var enemy1 = state!.Combatants.First(c => c.Id == 201);
        var enemy2 = state.Combatants.First(c => c.Id == 202);

        // Ambos devem ter levado dano mágico
        enemy1.CurrentHP.ShouldBeLessThan(100);
        enemy2.CurrentHP.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task BloodFuel_ShouldCostHP_GiveVigor_AndApplyBuff()
    {
        var (combatId, mediator, repo) = await SetupVexCombat(new());

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_VEX_FUEL",
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new()
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var vex = state!.Combatants.First(c => c.Id == 101);
        var player = state.Players.First(p => p.PlayerId == 1);

        // 1. HP Cost
        vex.CurrentHP.ShouldBe(125); // 140 - 15

        // 2. Essence Gain
        player.EssencePool[EssenceType.Vigor].ShouldBe(2);

        // 3. Buff Applied
        vex.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_VEX_ADRENALINE");
    }

    [Fact]
    public async Task Decimate_ShouldHealVex_IfEnemyDies()
    {
        var (combatId, mediator, repo) = await SetupVexCombat(new() { { EssenceType.Vigor, 2 }, { EssenceType.Flux, 1 } });

        var stateInicial = await repo.GetAsync(combatId);
        var enemy = stateInicial!.Combatants.First(c => c.Id == 201);
        var vex = stateInicial.Combatants.First(c => c.Id == 101);

        // Setup: Vex ferido, Inimigo moribundo
        vex.CurrentHP = 50;
        enemy.CurrentHP = 10;
        await repo.SaveAsync(combatId, stateInicial);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_VEX_ULTI",
            TargetSelections = new() { { "SELF", new List<int> { 101 } }, { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Vigor, 2 }, { EssenceType.Flux, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var enemyResult = state!.Combatants.First(c => c.Id == 201);
        var vexResult = state.Combatants.First(c => c.Id == 101);

        // 1. Inimigo morreu
        enemyResult.IsAlive.ShouldBeFalse();

        // 2. Vex curou-se (Trigger ON_DEATH do Modifier aplicado pelo Ulti)
        // Base 30 + (Attack 14 * 0.5) = 37 Cura.
        vexResult.CurrentHP.ShouldBe(87);
    }
}