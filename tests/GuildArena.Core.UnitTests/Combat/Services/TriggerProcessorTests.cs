using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class TriggerProcessorTests
{
    private readonly IModifierDefinitionRepository _modRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly ICombatEngine _combatEngine;
    private readonly ILogger<TriggerProcessor> _logger;
    private readonly TriggerProcessor _processor;

    public TriggerProcessorTests()
    {
        _modRepo = Substitute.For<IModifierDefinitionRepository>();
        _abilityRepo = Substitute.For<IAbilityDefinitionRepository>();
        _combatEngine = Substitute.For<ICombatEngine>();
        _logger = Substitute.For<ILogger<TriggerProcessor>>();

        // Usamos Lazy para simular a injeção real e evitar ciclo
        var lazyEngine = new Lazy<ICombatEngine>(() => _combatEngine);

        _processor = new TriggerProcessor(_modRepo, lazyEngine, _logger, _abilityRepo);
    }

    [Fact]
    public void ProcessTriggers_WhenConditionMet_ShouldExecuteAbility()
    {
        // ARRANGE
        var modId = "MOD_THORNS";
        var abilityId = "ABIL_COUNTER";

        // 1. Definição do Modifier
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Thorns",
            Triggers = new() { ModifierTrigger.ON_RECEIVE_MELEE_ATTACK },
            TriggeredAbilityId = abilityId
        };
        _modRepo.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        // 2. Definição da Habilidade Triggered
        var abilityDef = new AbilityDefinition
        {
            Id = abilityId,
            Name = "Counter Strike",
            // Adicionar regra para garantir consistência lógica (opcional para este erro, mas boa prática)
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } }
        };

        _abilityRepo.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => {
                x[1] = abilityDef;
                return true;
            });

        // 3. Estado do Jogo
        // CORREÇÃO AQUI: Definir CurrentHP > 0 para IsAlive ser true
        var attacker = new Combatant { Id = 1, Name = "Attacker", BaseStats = new(), CurrentHP = 100 };
        var defender = new Combatant { Id = 2, Name = "Defender", BaseStats = new(), CurrentHP = 100 };

        defender.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var gameState = new GameState { Combatants = new() { attacker, defender } };

        // 4. Contexto do Evento
        var context = new TriggerContext
        {
            Source = attacker,
            Target = defender,
            GameState = gameState,
            Tags = new() { "Melee" }
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MELEE_ATTACK, context);

        // ASSERT
        _combatEngine.Received(1).ExecuteAbility(
            gameState,
            abilityDef,
            defender,
            Arg.Any<AbilityTargets>(),
            Arg.Any<Dictionary<EssenceType, int>>()
        );
    }

    [Fact]
    public void ProcessTriggers_WhenHolderIsNotTarget_ShouldNotExecute()
    {
        // ARRANGE
        // Cenário: O Jogador A tem "Thorns", mas quem levou dano foi o Jogador B.
        // O Trigger ON_RECEIVE_MELEE_ATTACK dispara globalmente, mas o processador deve filtrar.
        var modId = "MOD_THORNS";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Thorns", // Propriedade required adicionada
            Triggers = new() { ModifierTrigger.ON_RECEIVE_MELEE_ATTACK },
            TriggeredAbilityId = "ABIL_COUNTER"
        };
        _modRepo.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var holder = new Combatant { Id = 1, Name = "Holder", BaseStats = new() };
        holder.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var victim = new Combatant { Id = 2, Name = "Victim", BaseStats = new() }; // Outra pessoa
        var attacker = new Combatant { Id = 3, Name = "Attacker", BaseStats = new() };

        var gameState = new GameState { Combatants = new() { holder, victim, attacker } };

        var context = new TriggerContext
        {
            Source = attacker,
            Target = victim, // A vítima NÃO é o holder
            GameState = gameState
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MELEE_ATTACK, context);

        // ASSERT
        _combatEngine.DidNotReceive().ExecuteAbility(Arg.Any<GameState>(), Arg.Any<AbilityDefinition>(), Arg.Any<Combatant>(), Arg.Any<AbilityTargets>(), Arg.Any<Dictionary<EssenceType, int>>());
    }

    [Fact]
    public void ProcessTriggers_GlobalTrigger_ShouldExecuteForEveryoneWithMod()
    {
        // ARRANGE
        // Cenário: "Soul Harvest" - Sempre que alguém morre (ON_DEATH), ganha Stack.
        var modId = "MOD_SOUL_HARVEST";
        var abilityId = "ABIL_ADD_STACK";

        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Soul Harvest", // Propriedade required adicionada
            Triggers = new() { ModifierTrigger.ON_DEATH }, // Trigger Global
            TriggeredAbilityId = abilityId
        };
        _modRepo.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var abilityDef = new AbilityDefinition { Id = abilityId, Name = "Stack" };
        _abilityRepo.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>()).Returns(x => { x[1] = abilityDef; return true; });

        var necromancer = new Combatant { Id = 1, Name = "Necro", BaseStats = new() };
        necromancer.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var dyingUnit = new Combatant { Id = 2, Name = "Peasant", BaseStats = new(), CurrentHP = 0 };

        var gameState = new GameState { Combatants = new() { necromancer, dyingUnit } };

        var context = new TriggerContext
        {
            Source = dyingUnit, // Quem morreu
            Target = dyingUnit,
            GameState = gameState
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_DEATH, context);

        // ASSERT
        // O Necromancer deve ativar a habilidade, mesmo não sendo ele o alvo ou a source do evento
        _combatEngine.Received(1).ExecuteAbility(
            gameState,
            abilityDef,
            necromancer,
            Arg.Any<AbilityTargets>(),
            Arg.Any<Dictionary<EssenceType, int>>()
        );
    }
}