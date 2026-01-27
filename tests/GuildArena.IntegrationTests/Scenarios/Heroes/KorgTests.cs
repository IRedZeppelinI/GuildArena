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
using NSubstitute;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.Heroes;

public class KorgTests : IntegrationTestBase
{
    // Helper para Setup
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupKorgCombat(
        Dictionary<EssenceType, int> korgEssence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // Korg (Player 1)
        var korg = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Korg",
            RaceId = "RACE_VALDRIN",
            CurrentHP = 200,
            MaxHP = 200,
            BaseStats = new BaseStats { Attack = 10, Defense = 10, Agility = 2, Magic = 8 },
            Position = 1,
            // Traits Passivos (Racial + Pessoal)
            ActiveModifiers = new List<ActiveModifier>
            {
                new ActiveModifier { DefinitionId = "MOD_RACIAL_VALDRIN_SKIN", TurnsRemaining = -1 },
                new ActiveModifier { DefinitionId = "MOD_KORG_TRAIT", TurnsRemaining = -1, CasterId = 101 }
            },
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_KORG_STONE_FIST", Name = "Stone Fist" }, 
                new() { Id = "ABIL_KORG_SHARD", Name = "Shard" },
                new() { Id = "ABIL_KORG_LINK", Name = "Link" },
                new() { Id = "ABIL_KORG_FORTRESS", Name = "Fortress" }
            }
        };

        // Ally (Player 1)
        var ally = new Combatant
        {
            Id = 102,
            OwnerId = 1,
            Name = "Ally",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats(),
            Position = 2
        };

        // Enemy (Player 2)
        var enemy = new Combatant
        {
            Id = 201,
            OwnerId = 2,
            Name = "Enemy",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 0, Attack = 10, MaxActions = 2 },
            Position = 1,
            Abilities = new List<AbilityDefinition>
            {
                new() { Id = "ABIL_COMMON_SLASH", Name = "Slash" }
            }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { korg, ally, enemy },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, EssencePool = korgEssence },
                new() { PlayerId = 2, EssencePool = new() }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task StoneFist_ShouldDealPhysicalDamage()
    {
        // Custo: 0 Essence.
        var (combatId, mediator, repo) = await SetupKorgCombat(new());

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_KORG_STONE_FIST",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new()
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == 201);

        // Dano: Base 8 + (10 Attack * 1.0) = 18.
        target.CurrentHP.ShouldBe(82); // 100 - 18
    }

    [Fact]
    public async Task TremblingStrike_ShouldDealDamage_AndApplyConcussion()
    {
        // Custo: 1 Vigor.
        var (combatId, mediator, repo) = await SetupKorgCombat(new() { { EssenceType.Vigor, 2 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_KORG_SHARD",
            TargetSelections = new() { { "TGT", new List<int> { 201 } } },
            Payment = new() { { EssenceType.Vigor, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var target = state!.Combatants.First(c => c.Id == 201);

        // 1. Dano: Base 10 + (10 Attack * 0.8) = 18.
        target.CurrentHP.ShouldBe(82);

        // 2. Debuff: Concussion (-20% Agility)
        target.ActiveModifiers.ShouldContain(m => m.DefinitionId == "MOD_KORG_CONCUSSION");
    }

    [Fact]
    public async Task GuardiansLink_ShouldShieldAlly_ScalingWithDefense()
    {
        // Custo: 1 Vigor + 1 Light.
        var (combatId, mediator, repo) = await SetupKorgCombat(
            new() { { EssenceType.Vigor, 2 }, { EssenceType.Light, 2 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_KORG_LINK",
            TargetSelections = new() { { "TGT", new List<int> { 102 } } }, // Alvo: Ally (102)
            Payment = new() { { EssenceType.Vigor, 1 }, { EssenceType.Light, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var ally = state!.Combatants.First(c => c.Id == 102);
        var barrierMod = ally.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_KORG_LINK_BARRIER");

        barrierMod.ShouldNotBeNull();

        // Validação Scaling:
        // Base 10 + (Korg Defense 10 * Scaling 1.5) = 25.
        barrierMod.CurrentBarrierValue.ShouldBe(25);
    }

    [Fact]
    public async Task FortressForm_ShouldTauntSelectedEnemy()
    {
        // Custo: 2 Vigor + 1 Light.
        var (combatId, mediator, repo) = await SetupKorgCombat(
            new() { { EssenceType.Vigor, 5 }, { EssenceType.Light, 5 } });

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 101,
            AbilityId = "ABIL_KORG_FORTRESS",
            TargetSelections = new()
            {
                { "SELF", new List<int> { 101 } },
                { "TGT_TAUNT", new List<int> { 201 } } // Alvo único para Taunt
            },
            Payment = new() { { EssenceType.Vigor, 2 }, { EssenceType.Light, 1 } }
        };

        await mediator.Send(command, CancellationToken.None);

        var state = await repo.GetAsync(combatId);
        var korg = state!.Combatants.First(c => c.Id == 101);
        var enemy = state.Combatants.First(c => c.Id == 201);

        // 1. Korg ganha Barreira Grande (Base 50 + (10*2.0) = 70)
        var korgBuff = korg.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_KORG_FORTRESS_BUFF");
        korgBuff.ShouldNotBeNull();
        korgBuff.CurrentBarrierValue.ShouldBe(70);

        // 2. Inimigo ganha Debuff "Provoked" com status Taunted e CasterId = Korg
        var tauntDebuff = enemy.ActiveModifiers.FirstOrDefault(m => m.ActiveStatusEffects.Contains(StatusEffectType.Taunted));
        tauntDebuff.ShouldNotBeNull();
        tauntDebuff.DefinitionId.ShouldBe("MOD_KORG_PROVOKED");
        tauntDebuff.CasterId.ShouldBe(101);
    }

    [Fact]
    public async Task Trait_ReactiveArmor_ShouldStackDefense_OnHit()
    {
        // Testar a reação passiva
        var (combatId, mediator, repo) = await SetupKorgCombat(new());
        var state = await repo.GetAsync(combatId);

        // Simular vez do inimigo
        state!.CurrentPlayerId = 2;
        var userMock = GetService<ICurrentUserService>();
        userMock.UserId.Returns(2);
        await repo.SaveAsync(combatId, state);

        // Inimigo ataca Korg com Slash (ID 201 -> 101)
        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = 201,
            AbilityId = "ABIL_COMMON_SLASH",
            TargetSelections = new() { { "TGT", new List<int> { 101 } } },
            Payment = new()
        };

        // HIT 1
        await mediator.Send(command, CancellationToken.None);
        state = await repo.GetAsync(combatId);
        var korg = state!.Combatants.First(c => c.Id == 101);

        // Verifica Stack 1
        var buff = korg.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_KORG_HARDEN");
        buff.ShouldNotBeNull();
        buff.StackCount.ShouldBe(1);

        // HIT 2
        await mediator.Send(command, CancellationToken.None);
        state = await repo.GetAsync(combatId);
        korg = state!.Combatants.First(c => c.Id == 101);

        // Verifica Stack 2
        buff = korg.ActiveModifiers.First(m => m.DefinitionId == "MOD_KORG_HARDEN");
        buff.StackCount.ShouldBe(2);
    }
}