using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class DeathServiceTests
{
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly IModifierDefinitionRepository _modifierRepoMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly ILogger<DeathService> _loggerMock;
    private readonly DeathService _service;

    public DeathServiceTests()
    {
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _modifierRepoMock = Substitute.For<IModifierDefinitionRepository>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _loggerMock = Substitute.For<ILogger<DeathService>>();

        _service = new DeathService(
            _triggerProcessorMock,
            _modifierRepoMock,
            _battleLogMock,
            _loggerMock);
    }

    [Fact]
    public void ProcessDeathIfApplicable_WhenTargetSurvives_ShouldDoNothing()
    {
        // ARRANGE
        var victim = CreateCombatant(1, 10); // 10 HP
        var killer = CreateCombatant(2, 100);
        var state = new GameState();

        // ACT
        _service.ProcessDeathIfApplicable(victim, killer, state);

        // ASSERT
        // Não deve disparar triggers nem logs
        _triggerProcessorMock.DidNotReceiveWithAnyArgs().ProcessTriggers(default, default!);
        _battleLogMock.DidNotReceiveWithAnyArgs().Log(default!);

        // HP mantém-se
        victim.CurrentHP.ShouldBe(10);
    }

    [Fact]
    public void ProcessDeathIfApplicable_WhenHPZero_ShouldClampAndTriggerDeath()
    {
        // ARRANGE
        var victim = CreateCombatant(1, -5); // HP Negativo
        var killer = CreateCombatant(2, 100);
        var state = new GameState();

        // ACT
        _service.ProcessDeathIfApplicable(victim, killer, state);

        // ASSERT
        // 1. Clamp
        victim.CurrentHP.ShouldBe(0);

        // 2. Log
        _battleLogMock.Received(1)
            .Log(Arg.Is<string>(s => s.Contains("has been slain")));

        // 3. Trigger ON_DEATH
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEATH,
            Arg.Is<TriggerContext>(c => c.Target!.Id == victim.Id && c.Source.Id == killer.Id));
    }

    [Fact]
    public void ProcessDeath_ShouldRemoveLinkedModifiers_WhenConfiguredInJSON()
    {
        // ARRANGE
        // Cenário: Korg Morre. O Taunt no inimigo deve desaparecer.
        var deadKorg = CreateCombatant(1, 0); // Korg
        var enemy = CreateCombatant(2, 100);  // Inimigo Taunted

        // Configurar Modifier no Inimigo (Caster = Korg)
        var tauntModId = "MOD_TAUNTED";
        enemy.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = tauntModId,
            CasterId = deadKorg.Id,
            TurnsRemaining = 2
        });

        var state = new GameState { Combatants = { deadKorg, enemy } };

        // Mock do Repo para dizer que este mod deve ser removido
        var modDef = new ModifierDefinition
        {
            Id = tauntModId,
            Name = "Taunted",
            RemoveOnCasterDeath = true // <--- A CONFIGURAÇÃO CRÍTICA
        };
        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { tauntModId, modDef }
        });

        // ACT
        _service.ProcessDeathIfApplicable(deadKorg, CreateCombatant(3, 100), state);

        // ASSERT
        enemy.ActiveModifiers.ShouldBeEmpty
            ("Modifier should be removed because Caster died and RemoveOnCasterDeath is true.");
    }

    [Fact]
    public void ProcessDeath_ShouldKeepLinkedModifiers_WhenNotConfigured()
    {
        // ARRANGE
        // Cenário: Mago morre, mas o DoT (Poison) que ele meteu no inimigo deve continuar.
        var deadMage = CreateCombatant(1, 0);
        var enemy = CreateCombatant(2, 100);

        var poisonModId = "MOD_POISON";
        enemy.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = poisonModId,
            CasterId = deadMage.Id,
            TurnsRemaining = 3
        });

        var state = new GameState { Combatants = { deadMage, enemy } };

        // Mock do Repo: RemoveOnCasterDeath = false (Default)
        var modDef = new ModifierDefinition
        {
            Id = poisonModId,
            Name = "Poison",
            RemoveOnCasterDeath = false // <--- NÃO REMOVER
        };
        _modifierRepoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { poisonModId, modDef }
        });

        // ACT
        _service.ProcessDeathIfApplicable(deadMage, CreateCombatant(3, 100), state);

        // ASSERT
        enemy.ActiveModifiers.Count.ShouldBe(1);
        enemy.ActiveModifiers.First().DefinitionId.ShouldBe(poisonModId);
    }

    [Fact]
    public void ProcessDeath_InternalCleanup_ShouldRemoveTemps_AndKeepTraits()
    {
        // ARRANGE
        var victim = CreateCombatant(1, -10);

        // 1. Buff Temporário (ex: Attack Up) - Deve desaparecer
        victim.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_TEMP",
            TurnsRemaining = 2
        });

        // 2. Trait Permanente (ex: Racial) - Deve ficar (para ressurreição)
        victim.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_PASSIVE",
            TurnsRemaining = -1
        });

        var state = new GameState { Combatants = { victim } };

        // ACT
        _service.ProcessDeathIfApplicable(victim, CreateCombatant(2, 100), state);

        // ASSERT
        victim.ActiveModifiers.Count.ShouldBe(1);
        victim.ActiveModifiers.First().DefinitionId.ShouldBe("MOD_PASSIVE");
        victim.ActiveModifiers.First().TurnsRemaining.ShouldBe(-1);
    }

    // --- HELPER ---
    private Combatant CreateCombatant(int id, int hp)
    {
        return new Combatant
        {
            Id = id,
            Name = $"Unit_{id}",
            RaceId = "RACE_X",
            CurrentHP = hp,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
    }
}