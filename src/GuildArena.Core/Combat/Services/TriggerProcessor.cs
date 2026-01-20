using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Targeting;
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

            // Lista para guardar os modifiers que se "gastaram" e devem ser removidos
            var modifiersToRemove = new List<ActiveModifier>();


            foreach (var activeMod in combatant.ActiveModifiers)
            {
                if (!allDefinitions.TryGetValue(activeMod.DefinitionId, out var def)) continue;

                if (!def.Triggers.Contains(trigger)) continue;

                // Valida se o trigger pertence a este combatente
                if (!ValidateCondition(trigger, combatant, context, def)) continue;

                if (!string.IsNullOrEmpty(def.TriggeredAbilityId))
                {
                    ExecuteTriggeredAbility(def.TriggeredAbilityId, combatant, context);
                }

                if (def.RemoveAfterTrigger)
                {
                    modifiersToRemove.Add(activeMod);
                }
            }

            foreach (var mod in modifiersToRemove)
            {
                combatant.ActiveModifiers.Remove(mod);
                // Opcional: Logar que o efeito expirou/foi consumido
                _logger.LogDebug
                    ("Modifier {ModId} consumed on trigger {Trigger}.", mod.DefinitionId, trigger);
            }
        }
    }

    /// <summary>
    /// Validates if the context meets the logic requirements for the modifier holder.
    /// Handles both Self-Triggers (Standard) and Observer-Triggers (Allies/Enemies).
    /// </summary>
    private bool ValidateCondition(
        ModifierTrigger trigger,
        Combatant holder,
        TriggerContext context,
        ModifierDefinition def)
    {
        // 1. Triggers Ofensivos (Ações causadas pelo Holder)
        if (IsOffensiveTrigger(trigger))
        {
            // O trigger só dispara se o portador do modifier for a FONTE do evento.
            // Ex: Garret ataca -> Garret ativa trait.
            return context.Source.Id == holder.Id;
        }

        // 2. Triggers Defensivos / Reativos (Ações recebidas)
        if (IsDefensiveTrigger(trigger))
        {
            // Caso Base: O holder é o alvo do evento? (Ex: Korg leva dano)
            if (context.Target?.Id == holder.Id)
            {
                return true;
            }

            // Caso Observer: O holder não é o alvo, mas configurou flags para observar outros.
            if (context.Target != null)
            {
                bool isAlly = context.Target.OwnerId == holder.OwnerId;

                // Observar Aliados (ex: Elysia vê Korg ser curado)
                if (def.TriggerOnAllies && isAlly)
                {
                    return true;
                }

                // Observar Inimigos (ex: Bloodlust quando inimigo sangra)
                if (def.TriggerOnEnemies && !isAlly)
                {
                    return true;
                }
            }

            return false;
        }

        // Para triggers globais (ON_TURN_START, etc), assume-se true se passou os filtros iniciais.
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

    //private AbilityTargets ResolveAutoTargets(
    //    TriggerContext context,
    //    AbilityDefinition ability,
    //    Combatant source)
    //{
    //    var targets = new AbilityTargets();

    //    foreach (var rule in ability.TargetingRules)
    //    {
    //        // Se a regra pede Inimigo, e o contexto tem um atacante (Source), usamos esse.
    //        if (rule.Type == TargetType.Enemy && context.Source.Id != source.Id)
    //        {
    //            targets.SelectedTargets[rule.RuleId] = new List<int> { context.Source.Id };
    //        }
    //        // Se a regra pede Self/Friendly, e o contexto tem Target, usamos esse
    //        else if ((rule.Type == TargetType.Self || rule.Type == TargetType.Friendly) 
    //            && context.Target != null)
    //        {
    //            targets.SelectedTargets[rule.RuleId] = new List<int> { source.Id };
    //        }
    //    }
    //    return targets;
    //}

    /// <summary>
    /// Intelligently resolves targets for reactive abilities based on the trigger context.
    /// </summary>
    private AbilityTargets ResolveAutoTargets(
        TriggerContext context,
        AbilityDefinition ability,
        Combatant holder)
    {
        var targets = new AbilityTargets();

        foreach (var rule in ability.TargetingRules)
        {
            // CASO A: Vingança / Counter-Attack (Ex: Thorns)
            // A regra pede um Inimigo. O evento foi causado por alguém que não eu?
            // Alvo = A FONTE do evento (quem bateu).
            if (rule.Type == TargetType.Enemy && context.Source.Id != holder.Id)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { context.Source.Id };
            }

            // CASO B: Suporte / Proteção (Ex: Elysia Trait)
            // A regra pede um Aliado/Friendly. O evento aconteceu a alguém?
            // Alvo = O ALVO do evento (quem foi curado/atacado).
            // Isto garante que a Elysia buffa o Korg (Target) e não a si mesma (Holder).
            else if (
                (rule.Type == TargetType.Self || 
                rule.Type == TargetType.Friendly || 
                rule.Type == TargetType.Ally) && context.Target != null)
            {
                // Nota: Se for um Self-Trigger normal (Holder == Target), isto também funciona corretamente.
                targets.SelectedTargets[rule.RuleId] = new List<int> { context.Target.Id };
            }

            // CASO C: Fallback genérico para Self (caso não haja contexto de alvo externo)
            else if (rule.Type == TargetType.Self)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { holder.Id };
            }
        }
        return targets;
    }

    // --- HELPERS 

    private static bool IsDefensiveTrigger(ModifierTrigger trigger)
    {
        return trigger == ModifierTrigger.ON_RECEIVE_DAMAGE ||
               trigger == ModifierTrigger.ON_RECEIVE_PHYSICAL_DAMAGE ||
               trigger == ModifierTrigger.ON_RECEIVE_MAGIC_DAMAGE ||
               trigger == ModifierTrigger.ON_RECEIVE_MELEE_ATTACK ||
               trigger == ModifierTrigger.ON_RECEIVE_RANGED_ATTACK ||
               trigger == ModifierTrigger.ON_RECEIVE_SPELL_ATTACK ||
               trigger == ModifierTrigger.ON_RECEIVE_HEAL;
    }

    private static bool IsOffensiveTrigger(ModifierTrigger trigger)
    {
        return trigger == ModifierTrigger.ON_ABILITY_CAST ||
               trigger == ModifierTrigger.ON_DEAL_DAMAGE ||
               trigger == ModifierTrigger.ON_DEAL_PHYSICAL_DAMAGE ||
               trigger == ModifierTrigger.ON_DEAL_MAGIC_DAMAGE ||
               trigger == ModifierTrigger.ON_DEAL_MELEE_ATTACK ||
               trigger == ModifierTrigger.ON_DEAL_RANGED_ATTACK ||
               trigger == ModifierTrigger.ON_DEAL_SPELL_ATTACK ||
               trigger == ModifierTrigger.ON_DEAL_HEAL;
    }
}