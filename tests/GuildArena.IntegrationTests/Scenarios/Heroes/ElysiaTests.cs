using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.Heroes;

public class ElysiaTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupElysiaCombat(
        Dictionary<EssenceType, int> essence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // 1. Elysia (Healer/Buffer)
        var elysia = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Elysia",
            RaceId = "RACE_PSYLIAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Magic = 10, MagicDefense = 10, MaxActions = 1 },
            Position = 1,
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_ELYSIA_TRAIT", TurnsRemaining = -1, CasterId = 101 }
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_ELYSIA_BOLT", Name = "Bolt" },
                new() { Id = "ABIL_ELYSIA_SHOCK", Name = "Shock" },
                new() { Id = "ABIL_ELYSIA_MEND", Name = "Mend", ActionPointCost = 1 },
                new() { Id = "ABIL_ELYSIA_ULTI", Name = "Ulti" }
            }
        };

        // 2. Tank (Ferido para testes)
        var tank = new Combatant
        {
            Id = 102,
            OwnerId = 1,
            Name = "Ally Tank",
            RaceId = "RACE_HUMAN",
            CurrentHP = 50,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 5 },
            Position = 2
        };

        // 3. Observer (Full HP - para testar que não ganha buff se não curar)
        var fullHpUnit = new Combatant
        {
            Id = 103,
            OwnerId = 1,
            Name = "Full HP Unit",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats(),
            Position = 3
        };

        // 4. Enemy
        var enemy = new Combatant
        {
            Id = 201,
            OwnerId = 2,
            Name = "Enemy",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { MagicDefense = 2 },
            Position = 1
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { elysia, tank, fullHpUnit, enemy },
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
    public async Task PsionicBolt_ShouldDealMagicDamage()
    {
        var (combatId, mediator, repo) = await SetupElysiaCombat(new());

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_ELYSIA_BOLT",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new()
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == 201);

        // 8 Base + 10 Magic - 2 Res = 16 Dano
        target.CurrentHP.ShouldBe(84);
    }

    [Fact]
    public async Task MindShock_ShouldDealDamage_AndSilence()
    {
        var (combatId, mediator, repo) = await SetupElysiaCombat(
            new() { { EssenceType.Mind, 1 }, { EssenceType.Neutral, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_ELYSIA_SHOCK",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Mind, 1 }, { EssenceType.Neutral, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == 201);

        target.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_SILENCE");
    }

    [Fact]
    public async Task MendingLight_ShouldHeal_WithCorrectScaling()
    {
        // Este teste foca-se na matemática da cura, isolado do Trait.
        var (combatId, mediator, repo) = await SetupElysiaCombat(
            new() { { EssenceType.Mind, 1 }, { EssenceType.Light, 1 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_ELYSIA_MEND",
            TargetSelections = new() { { "TGT", new List<int> { 102 } } }, // Tank (50 HP)
            Payment = new() { { EssenceType.Mind, 1 }, { EssenceType.Light, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var tank = state!.Combatants.First(c => c.Id == 102);

        // Base 10 + (Magic 10 * 1.5 Scale) = 25 Cura
        tank.CurrentHP.ShouldBe(75); // 50 + 25
    }

    [Fact]
    public async Task Trait_ShouldStackBuff_OnRepeatedHeals()
    {
        var resources = new Dictionary<EssenceType, int>
        {
            { EssenceType.Mind, 2 }, { EssenceType.Light, 2 }
        };

        var (combatId, mediator, repo) = await SetupElysiaCombat(resources);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_ELYSIA_MEND",
            TargetSelections = new() { { "TGT", new List<int> { 102 } } },
            Payment = new() { { EssenceType.Mind, 1 }, { EssenceType.Light, 1 } }
        };

        // --- HEAL 1 ---
        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var tank = state!.Combatants.First(c => c.Id == 102);

        // Verifica Stack 1
        var buff = tank.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_ELYSIA_DEFENSE_BUFF");
        buff.ShouldNotBeNull();
        buff.StackCount.ShouldBe(1);

        // Reset AP
        var elysia = state.Combatants.First(c => c.Id == 101);
        elysia.ActionsTakenThisTurn = 0;
        await repo.SaveAsync(combatId, state);

        // --- HEAL 2 ---
        await mediator.Send(command, CancellationToken.None);

        state = await repo.GetAsync(combatId);
        tank = state!.Combatants.First(c => c.Id == 102);
        buff = tank.ActiveModifiers.First(m => m.DefinitionId == "MOD_ELYSIA_DEFENSE_BUFF");

        // Verifica Stack 2
        buff.StackCount.ShouldBe(2);
    }

    [Fact]
    public async Task AstralConvergence_ShouldApplyBuff_OnlyIfHealingOccurs()
    {
        var (combatId, mediator, repo) = await SetupElysiaCombat(
            new() { { EssenceType.Mind, 2 }, { EssenceType.Light, 2 } });

        // ARRANGE: Ferir a Elysia para ela receber cura e buff
        var stateInicial = await repo.GetAsync(combatId);
        stateInicial!.Combatants.First(c => c.Id == 101).CurrentHP = 50;
        await repo.SaveAsync(combatId, stateInicial);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_ELYSIA_ULTI",
            // Seleciona Todos (Elysia Ferida, Tank Ferido, FullHP Unit)
            TargetSelections = new() { { "TGT_ALL", new List<int> { 101, 102, 103 } } },
            Payment = new() { { EssenceType.Mind, 2 }, { EssenceType.Light, 2 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var elysia = state!.Combatants.First(c => c.Id == 101); // Ferida -> Curada
        var tank = state.Combatants.First(c => c.Id == 102);     // Ferido -> Curado
        var fullGuy = state.Combatants.First(c => c.Id == 103);  // Full HP -> Cura 0

        // 1. Elysia recebeu buff? (Sim)
        elysia.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_ELYSIA_DEFENSE_BUFF");

        // 2. Tank recebeu buff? (Sim)
        tank.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_ELYSIA_DEFENSE_BUFF");

        // 3. FullHP Unit recebeu buff? (NÃO - A tua regra de negócio lógica impede triggers se cura = 0)
        fullGuy.ActiveModifiers.ShouldNotContain(
            m => m.DefinitionId == "MOD_ELYSIA_DEFENSE_BUFF",
            "Units with Full HP received 0 healing, so Trait should NOT trigger on them.");
    }
}