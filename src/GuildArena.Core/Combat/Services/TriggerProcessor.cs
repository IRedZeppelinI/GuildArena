using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

public class TriggerProcessor : ITriggerProcessor
{
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;
    private readonly IActionQueue _actionQueue;
    private readonly ILogger<TriggerProcessor> _logger;

    public TriggerProcessor(
        IModifierDefinitionRepository modifierRepo,
        IAbilityDefinitionRepository abilityRepo,
        IActionQueue actionQueue,
        ILogger<TriggerProcessor> logger)
    {
        _modifierRepo = modifierRepo;
        _abilityRepo = abilityRepo;
        _actionQueue = actionQueue;
        _logger = logger;
    }

    /// <inheritdoc />
    public void ProcessTriggers(ModifierTrigger trigger, TriggerContext context)
    {
        var allDefinitions = _modifierRepo.GetAllDefinitions();

        foreach (var combatant in context.GameState.Combatants)
        {
            // Se estiver morto, só processa triggers de morte (ON_DEATH)
            if (!combatant.IsAlive && trigger != ModifierTrigger.ON_DEATH) continue;

            foreach (var activeMod in combatant.ActiveModifiers)
            {
                if (!allDefinitions.TryGetValue(activeMod.DefinitionId, out var def)) continue;

                if (!def.Triggers.Contains(trigger)) continue;
                if (!ValidateCondition(trigger, combatant, context)) continue;

                if (!string.IsNullOrEmpty(def.TriggeredAbilityId))
                {
                    ExecuteTriggeredAbility(def.TriggeredAbilityId, combatant, context);
                }
            }
        }
    }

    /// <summary>
    /// Validates if the context meets the logic requirements for the modifier holder.
    /// </summary>
    private bool ValidateCondition(ModifierTrigger trigger, Combatant holder, TriggerContext context)
    {
        // Lógica para triggers Defensivos (Quem recebe a ação)
        if (trigger == ModifierTrigger.ON_RECEIVE_MELEE_ATTACK ||
            trigger == ModifierTrigger.ON_RECEIVE_RANGED_ATTACK ||
            trigger == ModifierTrigger.ON_RECEIVE_SPELL_ATTACK)
        {
            // O trigger só dispara se o portador do modifier for o ALVO do evento
            return context.Target?.Id == holder.Id;
        }

        // Lógica para triggers Ofensivos (Quem causa a ação)
        if (trigger == ModifierTrigger.ON_DEAL_MELEE_ATTACK ||
            trigger == ModifierTrigger.ON_DEAL_RANGED_ATTACK ||
            trigger == ModifierTrigger.ON_DEAL_SPELL_ATTACK)
        {
            // O trigger só dispara se o portador do modifier for a FONTE do evento
            return context.Source.Id == holder.Id;
        }

        return true;
    }

    private void ExecuteTriggeredAbility(string abilityId, Combatant source, TriggerContext context)
    {
        // 1. Obter a definição da Habilidade
        if (!_abilityRepo.TryGetDefinition(abilityId, out var ability))
        {
            _logger.LogWarning("Triggered Ability {AbilityId} not found.", abilityId);
            return;
        }

        _logger.LogInformation(
            "Trigger: scheduling ability {AbilityId} for {Source}", abilityId, source.Name);

        // 2. Definir Alvos Automáticos
        // Triggers reativos (ex: Counter-Attack) visam a fonte do ataque original.
        var autoTargets = ResolveAutoTargets(context, ability, source);

        // 3. Criar a Ação
        // Triggers normalmente não têm custo de essence associado ao jogador (pagamento vazio).
        var payment = new Dictionary<EssenceType, int>();

        var action = new ExecuteAbilityAction(
            ability,
            source,
            autoTargets,
            payment,
            isTriggeredAction: true
        );

        // 4. Enqueue 
        _actionQueue.Enqueue(action);
    }

    private AbilityTargets ResolveAutoTargets(
        TriggerContext context,
        AbilityDefinition ability,
        Combatant source)
    {
        var targets = new AbilityTargets();

        foreach (var rule in ability.TargetingRules)
        {
            // Se a regra pede Inimigo, e o contexto tem um atacante (Source), usamos esse.
            if (rule.Type == TargetType.Enemy && context.Source.Id != source.Id)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { context.Source.Id };
            }
            // Se a regra pede Self/Friendly, e o contexto tem Target, usamos esse
            else if ((rule.Type == TargetType.Self || rule.Type == TargetType.Friendly) 
                && context.Target != null)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { source.Id };
            }
        }
        return targets;
    }
}