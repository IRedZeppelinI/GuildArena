using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Services;

public class TriggerProcessorTests
{
    private readonly IModifierDefinitionRepository _modRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IActionQueue _actionQueue;
    private readonly ILogger<TriggerProcessor> _logger;
    private readonly TriggerProcessor _processor;

    public TriggerProcessorTests()
    {
        _modRepo = Substitute.For<IModifierDefinitionRepository>();
        _abilityRepo = Substitute.For<IAbilityDefinitionRepository>();
        _actionQueue = Substitute.For<IActionQueue>();
        _logger = Substitute.For<ILogger<TriggerProcessor>>();

        _processor = new TriggerProcessor(_modRepo, _abilityRepo, _actionQueue, _logger);
    }

    [Fact]
    public void ProcessTriggers_WhenConditionMet_ShouldEnqueueAction()
    {
        // ARRANGE
        var modId = "MOD_THORNS";
        var abilityId = "ABIL_COUNTER";

        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Thorns",
            Triggers = new() { ModifierTrigger.ON_RECEIVE_MELEE_ATTACK },
            TriggeredAbilityId = abilityId
        };
        _modRepo.GetAllDefinitions().
            Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var abilityDef = new AbilityDefinition
        {
            Id = abilityId,
            Name = "Counter Strike",
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy } }
        };

        _abilityRepo.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => {
                x[1] = abilityDef;
                return true;
            });

        
        var attacker = new Combatant
        {
            Id = 1,
            Name = "Attacker",
            RaceId = "RACE_A",
            BaseStats = new(),
            CurrentHP = 100
        };
        var defender = new Combatant
        {
            Id = 2,
            Name = "Defender",
            RaceId = "RACE_B",
            BaseStats = new(),
            CurrentHP = 100
        };

        defender.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var gameState = new GameState { Combatants = new() { attacker, defender } };

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
        _actionQueue.Received(1).Enqueue(Arg.Is<ICombatAction>(action =>
            action is ExecuteAbilityAction &&
            action.Source.Id == defender.Id &&
            action.Name.Contains(abilityId)
        ));
    }

    [Fact]
    public void ProcessTriggers_WhenHolderIsNotTarget_ShouldNotEnqueue()
    {
        // ARRANGE
        var modId = "MOD_THORNS";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Thorns",
            Triggers = new() { ModifierTrigger.ON_RECEIVE_MELEE_ATTACK },
            TriggeredAbilityId = "ABIL_COUNTER"
        };
        _modRepo.GetAllDefinitions().Returns
            (new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        
        var holder = new Combatant
        {
            Id = 1,
            Name = "Holder",
            RaceId = "RACE_A",
            BaseStats = new(),
            CurrentHP = 100
        };
        holder.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var victim = new Combatant
        {
            Id = 2,
            Name = "Victim",
            RaceId = "RACE_B",
            BaseStats = new(),
            CurrentHP = 100
        };
        var attacker = new Combatant
        {
            Id = 3,
            Name = "Attacker",
            RaceId = "RACE_C",
            BaseStats = new(),
            CurrentHP = 100
        };

        var gameState = new GameState { Combatants = new() { holder, victim, attacker } };

        var context = new TriggerContext
        {
            Source = attacker,
            Target = victim,
            GameState = gameState
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MELEE_ATTACK, context);

        // ASSERT
        _actionQueue.DidNotReceive().Enqueue(Arg.Any<ICombatAction>());
    }

    [Fact]
    public void ProcessTriggers_GlobalTrigger_ShouldEnqueueForEveryoneWithMod()
    {
        // ARRANGE
        var modId = "MOD_SOUL_HARVEST";
        var abilityId = "ABIL_ADD_STACK";

        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Soul Harvest",
            Triggers = new() { ModifierTrigger.ON_DEATH },
            TriggeredAbilityId = abilityId
        };
        _modRepo.GetAllDefinitions().
            Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var abilityDef = new AbilityDefinition { Id = abilityId, Name = "Stack" };
        _abilityRepo.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>()).
            Returns(x => { x[1] = abilityDef; return true; });

        
        var necromancer = new Combatant
        {
            Id = 1,
            Name = "Necro",
            RaceId = "RACE_UNDEAD",
            BaseStats = new(),
            CurrentHP = 100
        };
        necromancer.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var dyingUnit = new Combatant
        {
            Id = 2,
            Name = "Peasant",
            RaceId = "RACE_HUMAN",
            BaseStats = new(),
            CurrentHP = 0
        };

        var gameState = new GameState { Combatants = new() { necromancer, dyingUnit } };

        var context = new TriggerContext
        {
            Source = dyingUnit,
            Target = dyingUnit,
            GameState = gameState
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_DEATH, context);

        // ASSERT
        _actionQueue.Received(1).Enqueue(Arg.Is<ICombatAction>(action =>
            action is ExecuteAbilityAction &&
            action.Source.Id == necromancer.Id
        ));
    }

    [Fact]
    public void ProcessTriggers_WithRemoveAfterTrigger_ShouldExecuteAndRemoveModifier()
    {
        // ARRANGE
        var modId = "MOD_ONE_TIME";
        var abilityId = "ABIL_TEST";

        // Definição com a nova flag 'RemoveAfterTrigger = true'
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "One Shot",
            Triggers = new() { ModifierTrigger.ON_COMBAT_START },
            TriggeredAbilityId = abilityId,
            RemoveAfterTrigger = true // <--- O que estamos a testar
        };

        _modRepo.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        // Setup da Ability (só para garantir que o fluxo normal ocorre antes da remoção)
        var abilityDef = new AbilityDefinition { Id = abilityId, Name = "Test Ability" };
        _abilityRepo.TryGetDefinition(abilityId, out Arg.Any<AbilityDefinition>())
            .Returns(x => { x[1] = abilityDef; return true; });

        // Combatente com o Modifier
        var combatant = new Combatant
        {
            Id = 1,
            Name = "Tester",
            RaceId = "RACE_TEST",
            BaseStats = new(),
            CurrentHP = 100
        };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var gameState = new GameState { Combatants = new() { combatant } };

        var context = new TriggerContext
        {
            Source = combatant,
            Target = combatant,
            GameState = gameState,
            Tags = new HashSet<string> { "StartCombat" }
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_COMBAT_START, context);

        // ASSERT
        // 1. A habilidade deve ter sido agendada na mesma
        _actionQueue.Received(1).Enqueue(Arg.Any<ICombatAction>());

        // 2. A lista de modifiers deve estar vazia (o modifier foi consumido)
        combatant.ActiveModifiers.ShouldBeEmpty("Modifier should have been removed after trigger.");
    }

    [Fact]
    public void ProcessTriggers_WithoutRemoveAfterTrigger_ShouldKeepModifier()
    {
        // ARRANGE
        var modId = "MOD_PERSISTENT";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Persistent",
            Triggers = new() { ModifierTrigger.ON_COMBAT_START },
            RemoveAfterTrigger = false // <--- Normal behavior
        };

        _modRepo.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var combatant = new Combatant { Id = 1, Name = "Tester", RaceId = "X", BaseStats = new() };
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var context = new TriggerContext
        {
            Source = combatant,
            Target = combatant,
            GameState = new GameState { Combatants = { combatant } }
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_COMBAT_START, context);

        // ASSERT
        combatant.ActiveModifiers.Count.ShouldBe(1);
        combatant.ActiveModifiers.First().DefinitionId.ShouldBe(modId);
    }
}