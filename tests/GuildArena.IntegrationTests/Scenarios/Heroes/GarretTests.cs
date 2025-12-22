using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using Shouldly;
using GuildArena.Domain.Enums.Modifiers;
using MediatR; // <--- Importante

namespace GuildArena.IntegrationTests.Scenarios.Heroes;

public class GarretTests : IntegrationTestBase
{
    // Agora retornamos IMediator em vez do Handler concreto
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupGarretDuel(
        Dictionary<EssenceType, int> playerEssence)
    {
        // Pedimos o Mediator ao container
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // ... (Criação de combatentes igual) ...
        var garret = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Garret",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Attack = 10, Agility = 10 },
            Position = 1
        };

        var dummy = new Combatant
        {
            Id = -1,
            OwnerId = 0,
            Name = "Dummy",
            RaceId = "RACE_VALDRIN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 0 },
            Position = 2
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { garret, dummy },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, EssencePool = playerEssence }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task Adrenaline_ShouldApplyBuff_AndConsumeNeutralCost()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Mind, 5 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_ADRENALINE",
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new() { { EssenceType.Mind, 1 } }
        };

        // ACT
        // Usamos o Mediator.Send, exatamente como o Controller faz
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var garret = state!.Combatants.First(c => c.Id == 101);
        var player = state.Players.First(p => p.PlayerId == 1);

        garret.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_GARRET_ADRENALINE");
        player.EssencePool[EssenceType.Mind].ShouldBe(4);
    }

    [Fact]
    public async Task PommelBash_ShouldDealDamage_AndApplyStun()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Vigor, 5 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_POMMEL",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() { { EssenceType.Vigor, 2 } }
        };

        // Capturamos os logs que o Mediator retorna (List<string>)
        var logs = await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        target.CurrentHP.ShouldBe(90);
        target.ActiveModifiers.ShouldContain(m => m.ActiveStatusEffects.Contains(StatusEffectType.Stun));

        logs.ShouldContain(s => s.Contains("afflicted by Stunned"));
    }

    [Fact]
    public async Task SlayersMercy_ShouldDealMassiveTrueDamage()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Vigor, 10 } });

        var state = await repo.GetAsync(combatId);
        state!.Combatants.First(c => c.Id == 101).BaseStats.Attack = 20;
        state.Combatants.First(c => c.Id == -1).BaseStats.Defense = 999;
        state.Combatants.First(c => c.Id == -1).CurrentHP = 200;
        await repo.SaveAsync(combatId, state);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_EXECUTE",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() { { EssenceType.Vigor, 3 } }
        };

        var logs = await mediator.Send(command, CancellationToken.None);

        var updatedState = await repo.GetAsync(combatId);
        var target = updatedState!.Combatants.First(c => c.Id == -1);

        target.CurrentHP.ShouldBe(130);
        logs.ShouldContain(s => s.Contains("took 70 damage"));
    }

    [Fact]
    public async Task BasicAttack_ShouldUseCommonDefinition_AndDealDamage()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new Dictionary<EssenceType, int>());

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_COMMON_SLASH",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new()
        };

        var logs = await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        target.CurrentHP.ShouldBe(85);
        logs.ShouldContain(s => s.Contains("Garret used Slash"));
    }

    [Fact]
    public async Task PrecisionStrike_ShouldDealHighDamage()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Vigor, 2 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_PRECISION",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() { { EssenceType.Vigor, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        target.CurrentHP.ShouldBe(73);
    }
}