using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.General;

public class CommonAbilitiesTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupBasicDuel()
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // Um combatente genérico (ex: um Soldado)
        var soldier = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Soldier",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Attack = 10, Defense = 5 },
            Position = 1,
            Abilities = new List<AbilityDefinition>
            {
                new AbilityDefinition { Id = "ABIL_COMMON_SLASH", Name = "Slash" }
            },
            SpecialAbility = new AbilityDefinition { Id = "ABIL_COMMON_GUARD", Name = "Guard" }
        };

        var dummy = new Combatant
        {
            Id = -1,
            OwnerId = 0,
            Name = "Dummy",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 0 },
            Position = 2
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { soldier, dummy },
            Players = new List<CombatPlayer>
            {
                // Player com recursos genéricos
                new() { PlayerId = 1, EssencePool = new() { { EssenceType.Vigor, 5 } } }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task Slash_ShouldDealPhysicalDamage_ScalingWithAttack()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupBasicDuel();

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_COMMON_SLASH", // Habilidade Básica
            TargetSelections = new() { { "TGT", new List<int> { -1 } } },
            Payment = new() // Custo Zero
        };

        // ACT
        var logs = await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Cálculo: Base 5 + (Attack 10 * 1.0 Scaling) = 15 Dano
        target.CurrentHP.ShouldBe(85); // 100 - 15

        logs.ShouldContain(s => s.Contains("used Slash"));
    }

    [Fact]
    public async Task Guard_ShouldApplyDefenseBuff_ToSelf()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupBasicDuel();

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_COMMON_GUARD", // Habilidade Defensiva
            TargetSelections = new() { { "SELF", new List<int> { 101 } } },
            Payment = new() // Custo Zero
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var soldier = state!.Combatants.First(c => c.Id == 101);

        // 1. Verificar se o Modifier foi aplicado
        var guardMod = soldier.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_COMMON_GUARD");
        guardMod.ShouldNotBeNull();

        // 2. Verificar duração (Guard dura 1 turno)
        guardMod.TurnsRemaining.ShouldBe(1);

        // 3. (Opcional) Poderíamos verificar se o stat de Defense subiu, 
        // mas isso seria testar o StatCalculationService. 
        // Aqui basta saber que o modifier "Guarding" está lá.
    }
}