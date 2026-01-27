using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
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

public class GarretTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupGarretDuel(
        Dictionary<EssenceType, int> playerEssence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        var garret = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Garret",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Attack = 10, Agility = 10 },
            Position = 1,
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier
                {
                    DefinitionId = "MOD_GARRET_TRAIT",
                    TurnsRemaining = -1,
                    CasterId = 101
                }
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_COMMON_SLASH", Name = "Slash" },
                new() { Id = "ABIL_GARRET_PRECISION", Name = "Precision" },
                new() { Id = "ABIL_GARRET_POMMEL", Name = "Pommel" },
                new() { Id = "ABIL_GARRET_ADRENALINE", Name = "Adrenaline" },
                new() { Id = "ABIL_GARRET_EXECUTE", Name = "Execute" }
            }
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
    public async Task BasicAttack_ShouldUseCommonDefinition_AndDealDamage_WithTrait()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupGarretDuel(new Dictionary<EssenceType, int>());

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
        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Validar Dano:
        // Slash Base: 5
        // Scaling: 10 Attack * 1.0 = 10
        // Trait: +2 (Physical)
        // Total: 17
        target.CurrentHP.ShouldBe(83); // 100 - 17
    }

    [Fact]
    public async Task PrecisionStrike_ShouldDealHighDamage_WithTrait()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Vigor, 2 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_PRECISION",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() { { EssenceType.Vigor, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Dano:
        // Base: 15
        // Scaling: 10 Attack * 1.2 = 12
        // Trait: +2 (Physical)
        // Total: 29
        target.CurrentHP.ShouldBe(71); // 100 - 29
    }

    [Fact]
    public async Task Adrenaline_ShouldApplyBuff_AndConsumeNeutralCost()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Mind, 5 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_ADRENALINE",
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new() { { EssenceType.Mind, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var garret = state!.Combatants.First(c => c.Id == 101);
        var player = state.Players.First(p => p.PlayerId == 1);

        // Verifica modificador e consumo de mana
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

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Dano:
        // Base: 5
        // Scaling: 10 Attack * 0.5 = 5
        // Trait: +2 (Physical)
        // Total: 12
        target.CurrentHP.ShouldBe(88); // 100 - 12

        // Status Check
        target.ActiveModifiers.ShouldContain(m => m.ActiveStatusEffects.Contains(StatusEffectType.Stun));
    }

    [Fact]
    public async Task SlayersMercy_ShouldDealMassiveTrueDamage()
    {
        var (combatId, mediator, repo) = await SetupGarretDuel(new() { { EssenceType.Vigor, 10 } });

        // Ajustar stats para este teste: Defesa 999 no inimigo para testar True Damage
        var state = await repo.GetAsync(combatId);
        var dummy = state!.Combatants.First(c => c.Id == -1);
        dummy.BaseStats.Defense = 999;
        dummy.CurrentHP = 200;

        await repo.SaveAsync(combatId, state);

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_GARRET_EXECUTE",
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() { { EssenceType.Vigor, 3 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var updatedState = await repo.GetAsync(combatId);
        var target = updatedState!.Combatants.First(c => c.Id == -1);

        // Dano:
        // Base: 40
        // Scaling: 10 Attack * 1.5 = 15
        // Trait: +2 (Porque a skill tem tag Physical)
        // Total: 57 True Damage
        target.CurrentHP.ShouldBe(143); // 200 - 57
    }
}