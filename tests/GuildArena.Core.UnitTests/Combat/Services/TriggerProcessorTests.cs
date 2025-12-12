using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions; 
using GuildArena.Core.Combat.Services;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;

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

        // 1. Definição do Modifier (Thorns reage a receber ataque melee)
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Thorns",
            Triggers = new() { ModifierTrigger.ON_RECEIVE_MELEE_ATTACK },
            TriggeredAbilityId = abilityId
        };
        _modRepo.GetAllDefinitions().
            Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        // 2. Definição da Habilidade
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

        // 3. Combatentes
        var attacker = new Combatant { Id = 1, Name = "Attacker", BaseStats = new(), CurrentHP = 100 };
        var defender = new Combatant { Id = 2, Name = "Defender", BaseStats = new(), CurrentHP = 100 };

        // O defensor tem os espinhos
        defender.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var gameState = new GameState { Combatants = new() { attacker, defender } };

        // 4. Contexto: O Attacker bateu no Defender
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
        // Verificamos se a fila recebeu UMA ação.
        // E verificamos se essa ação é do tipo
        // ExecuteAbilityAction e se a Source é o Defender (quem disparou os espinhos).
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
        // Cenário: O Holder tem "Thorns", mas quem levou a pancada foi a "Victim".
        // O ValidateCondition deve impedir que o Holder dispare.
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

        var holder = new Combatant { Id = 1, Name = "Holder", BaseStats = new(), CurrentHP = 100 };
        holder.ActiveModifiers.Add(new ActiveModifier { DefinitionId = modId });

        var victim = new Combatant { Id = 2, Name = "Victim", BaseStats = new(), CurrentHP = 100 };
        var attacker = new Combatant { Id = 3, Name = "Attacker", BaseStats = new(), CurrentHP = 100 };

        var gameState = new GameState { Combatants = new() { holder, victim, attacker } };

        var context = new TriggerContext
        {
            Source = attacker,
            Target = victim, // O alvo é a Vítima, não o Holder
            GameState = gameState
        };

        // ACT
        _processor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MELEE_ATTACK, context);

        // ASSERT
        // A fila NÃO deve receber nada
        _actionQueue.DidNotReceive().Enqueue(Arg.Any<ICombatAction>());
    }

    [Fact]
    public void ProcessTriggers_GlobalTrigger_ShouldEnqueueForEveryoneWithMod()
    {
        // ARRANGE
        // Cenário: "Soul Harvest" - Sempre que alguém morre (ON_DEATH), ganha Stack.
        // Este é um trigger que ignora o ValidateCondition restritivo (retorna true por default).
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

        var necromancer = new Combatant { Id = 1, Name = "Necro", BaseStats = new(), CurrentHP = 100 };
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
        // O Necromancer deve ter agendado a sua habilidade
        _actionQueue.Received(1).Enqueue(Arg.Is<ICombatAction>(action =>
            action is ExecuteAbilityAction &&
            action.Source.Id == necromancer.Id
        ));
    }
}