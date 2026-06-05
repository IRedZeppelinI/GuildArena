using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
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
            OwnerId = 2, // Alterado para a cadeira 2
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
                // AQUI ESTÁ A CORREÇÃO: Adicionamos o UserId da sessão de testes
                new() { PlayerId = 1, UserId = "user-123", EssencePool = new() { { EssenceType.Vigor, 5 } } },
                new() { PlayerId = 2, UserId = "enemy-456", EssencePool = new() }
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
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == -1);

        // Cálculo: Base 5 + (Attack 10 * 1.0 Scaling) = 15 Dano
        target.CurrentHP.ShouldBe(85); // 100 - 15        
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
    }    

    

    [Fact]
    public async Task Cleave_ShouldDealDamage_ToAllEnemies()
    {
        // ARRANGE
        // Attacker: Base Attack 10. Cleave Base Dmg 3 + (10 * 0.6) = 9 Raw Damage.
        var attacker = new Combatant { Id = 1, OwnerId = 1, Name = "Warlord", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Attack = 10 }, Abilities = new List<AbilityDefinition> { new() { Id = "ABIL_COMMON_CLEAVE", Name = "Cleave" } } };
        var def1 = new Combatant { Id = 2, OwnerId = 2, Name = "Target1", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Defense = 0 } };
        var def2 = new Combatant { Id = 3, OwnerId = 2, Name = "Target2", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Defense = 0 } };
        var def3 = new Combatant { Id = 4, OwnerId = 2, Name = "Target3", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Defense = 0 } };

        var (combatId, mediator, repo) = await SetupCustomScenario(new List<Combatant> { attacker, def1, def2, def3 });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 1,
            AbilityId = "ABIL_COMMON_CLEAVE",
            // Mesmo com a lista de alvos vazia, o Backend sabe que AoE atinge todos
            TargetSelections = new(),
            Payment = new() { { EssenceType.Vigor, 1 }, { EssenceType.Neutral, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);

        // Os 3 inimigos devem ter sofrido exatos 9 de dano
        state!.Combatants.First(c => c.Id == 2).CurrentHP.ShouldBe(91);
        state!.Combatants.First(c => c.Id == 3).CurrentHP.ShouldBe(91);
        state!.Combatants.First(c => c.Id == 4).CurrentHP.ShouldBe(91);
    }

    [Fact]
    public async Task PiercingArrow_ShouldIgnoreDefense()
    {
        // ARRANGE
        // Attacker: Agility 10. Piercing Base Dmg 4 + (10 * 1.0) = 14 Raw Damage.
        var attacker = new Combatant { Id = 1, OwnerId = 1, Name = "Archer", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Agility = 10 }, Abilities = new List<AbilityDefinition> { new() { Id = "ABIL_COMMON_PIERCING_ARROW", Name = "Piercing Arrow" } } };

        // Defender: Defense 10. Se fosse um ataque normal levava (14 - 10) = 4 dano.
        // Mas Piercing tem 20% DefensePenetration. Defesa efetiva = 10 * 0.8 = 8.
        // Dano esperado: 14 - 8 = 6.
        var defender = new Combatant { Id = 2, OwnerId = 2, Name = "Tank", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Defense = 10 } };

        var (combatId, mediator, repo) = await SetupCustomScenario(new List<Combatant> { attacker, defender });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 1,
            AbilityId = "ABIL_COMMON_PIERCING_ARROW",
            TargetSelections = new(), // Strategy é Random, ele escolhe o inimigo sozinho
            Payment = new()
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);
        state!.Combatants.First(c => c.Id == 2).CurrentHP.ShouldBe(94); // 100 - 6
    }

    [Fact]
    public async Task LesserHeal_ShouldTarget_LowestHpAlly()
    {
        // ARRANGE
        // Healer: Magic 10. Heal Base 10 + (10 * 1.0) = 20 HP restaurado.
        var healer = new Combatant { Id = 1, OwnerId = 1, Name = "Acolyte", RaceId = "RACE_PSYLIAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats { Magic = 10 }, Abilities = new List<AbilityDefinition> { new() { Id = "ABIL_COMMON_LESSER_HEAL", Name = "Mend" } } };

        // Aliado 1 está com vida cheia (100%)
        var ally1 = new Combatant { Id = 2, OwnerId = 1, Name = "Ally Full", RaceId = "RACE_HUMAN", CurrentHP = 100, MaxHP = 100, BaseStats = new BaseStats() };

        // Aliado 2 está quase a morrer (30%)
        var ally2 = new Combatant { Id = 3, OwnerId = 1, Name = "Ally Dying", RaceId = "RACE_HUMAN", CurrentHP = 30, MaxHP = 100, BaseStats = new BaseStats() };

        var (combatId, mediator, repo) = await SetupCustomScenario(new List<Combatant> { healer, ally1, ally2 });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 1,
            AbilityId = "ABIL_COMMON_LESSER_HEAL",
            // Não enviamos alvo na UI. A Strategy "LowestHPPercent" tem de tratar de tudo
            TargetSelections = new(),
            Payment = new() { { EssenceType.Light, 1 }, { EssenceType.Neutral, 1 } }
        };

        // ACT
        await mediator.Send(command, CancellationToken.None);

        // ASSERT
        var state = await repo.GetAsync(combatId);

        // Aliado 1 não deve ter recebido cura
        state!.Combatants.First(c => c.Id == 2).CurrentHP.ShouldBe(100);

        // Aliado 2 devia ter 30 HP e agora tem 50 (30 + 20 da cura)
        state!.Combatants.First(c => c.Id == 3).CurrentHP.ShouldBe(50);
    }



    // --- HELPER PARA CONSTRUIR CENÁRIOS RÁPIDOS ---
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupCustomScenario(
        List<Combatant> combatants)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = combatants,
            Players = new List<CombatPlayer>
            {
                new()
                {
                    PlayerId = 1,
                    UserId = "user-123", 
                    // Damos muita essence para o teste não falhar por falta de "mana"
                    EssencePool = new() {
                        { EssenceType.Vigor, 10 },
                        { EssenceType.Neutral, 10 },
                        { EssenceType.Light, 10 },
                        { EssenceType.Shadow, 10 },
                        { EssenceType.Flux, 10 },
                        { EssenceType.Mind, 10 }
                    }
                },
                new() { PlayerId = 2, UserId = "enemy-456", EssencePool = new() }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }
}