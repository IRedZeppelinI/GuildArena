using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.IntegrationTests.Setup;
using MediatR;
using NSubstitute;
using Shouldly;

namespace GuildArena.IntegrationTests.Scenarios.General;

public class BossAbilitiesTests : IntegrationTestBase
{
    private async Task<(string CombatId, IMediator Mediator, ICombatStateRepository Repo)> SetupBossEncounter(
        string bossAbilityId, Dictionary<EssenceType, int> playerEssence)
    {
        var mediator = GetService<IMediator>();
        var stateRepo = GetService<ICombatStateRepository>();
        var combatId = Guid.NewGuid().ToString();

        // O Jogador (Alvo)
        var hero = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Player Hero",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats { Defense = 5, MagicDefense = 5 },
            Position = 1
        };

        // O Boss
        var boss = new Combatant
        {
            Id = -1,
            OwnerId = 2,
            Name = "Boss",
            RaceId = "RACE_HUMAN",
            CurrentHP = 500,
            MaxHP = 500,
            BaseStats = new BaseStats { Attack = 10, Agility = 10, Magic = 10 },
            Position = 4,
            Abilities = new List<AbilityDefinition>
            {
                new AbilityDefinition { Id = bossAbilityId, Name = "Boss Skill" }
            }
        };

        var gameState = new GameState
        {
            CurrentPlayerId = 2, // Turno do Boss para ele atacar
            Combatants = new List<Combatant> { hero, boss },
            Players = new List<CombatPlayer>
            {
                new() { PlayerId = 1, UserId = "user-123", EssencePool = playerEssence },
                new() { PlayerId = 2, UserId = "enemy-456", EssencePool = new() }
            }
        };

        await stateRepo.SaveAsync(combatId, gameState);
        return (combatId, mediator, stateRepo);
    }

    [Fact]
    public async Task Kaelen_DirtyTactics_ShouldDealDamage_AndApplyBlindAndDisarm()
    {
        // ARRANGE
        var (combatId, mediator, repo) = await SetupBossEncounter("ABIL_BOSS_KAELEN_DIRTY_TACTICS", new());

        // CORREÇÃO: "Fazer login" como o inimigo para o backend autorizar o comando do Boss
        var userMock = GetService<ICurrentUserService>();
        userMock.UserId.Returns("enemy-456");

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = -1,
            AbilityId = "ABIL_BOSS_KAELEN_DIRTY_TACTICS",
            TargetSelections = new() { { "TGT", new List<int> { 101 } } }, // Boss ataca o Herói
            Payment = new()
        };

        // ACT
        var result = await mediator.Send(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue("O comando falhou as validações do motor (ex: Turno ou Custos).");

        var state = await repo.GetAsync(combatId);
        var hero = state!.Combatants.First(c => c.Id == 101);

        // Dano: Base 10 + (Agility 10 * 1.2) = 22. Defense 5 -> 17 Dano
        hero.CurrentHP.ShouldBe(83); // 100 - 17

        // Debuff: Deve ter Disarm e Blind
        var debuff = hero.ActiveModifiers.FirstOrDefault(m => m.DefinitionId == "MOD_BOSS_DIRTY_TACTICS");
        debuff.ShouldNotBeNull();
        debuff.ActiveStatusEffects.ShouldContain(StatusEffectType.Blind);
        debuff.ActiveStatusEffects.ShouldContain(StatusEffectType.Disarm);
        debuff.TurnsRemaining.ShouldBe(2);
    }

    [Fact]
    public async Task Malthus_EssenceDevour_ShouldDealMagicDamage_AndDrainPlayerEssence()
    {
        // ARRANGE
        // Damos 5 Vigor e 5 Mind ao jogador para o Boss ter o que roubar
        var startingEssence = new Dictionary<EssenceType, int>
        {
            { EssenceType.Vigor, 5 },
            { EssenceType.Mind, 5 }
        };

        var (combatId, mediator, repo) = await SetupBossEncounter("ABIL_BOSS_MALTHUS_DEVOUR", startingEssence);

        // CORREÇÃO: "Fazer login" como o inimigo para o backend autorizar o comando do Boss
        var userMock = GetService<ICurrentUserService>();
        userMock.UserId.Returns("enemy-456");

        var command = new ExecuteAbilityCommand
        {
            CombatId = combatId,
            SourceId = -1,
            AbilityId = "ABIL_BOSS_MALTHUS_DEVOUR",
            TargetSelections = new() { { "TGT", new List<int> { 101 } } },
            Payment = new()
        };

        // ACT
        var result = await mediator.Send(command, CancellationToken.None);

        // ASSERT
        result.IsSuccess.ShouldBeTrue("O comando falhou as validações do motor (ex: Turno ou Custos).");

        var state = await repo.GetAsync(combatId);
        var hero = state!.Combatants.First(c => c.Id == 101);
        var player = state.Players.First(p => p.PlayerId == 1);

        // Dano Mágico: Base 15 + (Magic 10 * 1.5) = 30. MagicDefense 5 -> 25 Dano
        hero.CurrentHP.ShouldBe(75); // 100 - 25

        // Essence Drain: O jogador começou com 10 no total (5 + 5). 
        // O efeito remove 2. Deve ter agora 8 no total.
        player.EssencePool.Values.Sum().ShouldBe(8);
    }
}