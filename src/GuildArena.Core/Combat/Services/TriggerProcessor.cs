using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Services;
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
    // Usamos Lazy para evitar Dependência Circular, pois o CombatEngine depende do TriggerProcessor
    private readonly Lazy<ICombatEngine> _combatEngine;
    private readonly ILogger<TriggerProcessor> _logger;

    public TriggerProcessor(
        IModifierDefinitionRepository modifierRepo,
        Lazy<ICombatEngine> combatEngine,
        ILogger<TriggerProcessor> logger,
        IAbilityDefinitionRepository abilityRepo)
    {
        _modifierRepo = modifierRepo;
        _combatEngine = combatEngine;
        _logger = logger;
        _abilityRepo = abilityRepo;
    }

    /// <inheritdoc />
    public void ProcessTriggers(ModifierTrigger trigger, TriggerContext context)
    {
        var allDefinitions = _modifierRepo.GetAllDefinitions();

        foreach (var combatant in context.GameState.Combatants)
        {
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
    /// E.g., If the trigger is ON_RECEIVE_DAMAGE, the holder must be the Target.
    /// </summary>
    private bool ValidateCondition(ModifierTrigger trigger, Combatant holder, TriggerContext context)
    {
        // Lógica para triggers Defensivos (Quem recebe a ação)
        if (trigger == ModifierTrigger.ON_RECEIVE_MELEE_ATTACK ||
            trigger == ModifierTrigger.ON_RECEIVE_RANGED_ATTACK ||
            trigger == ModifierTrigger.ON_RECEIVE_DAMAGE)
        {
            // O trigger só dispara se o portador do modifier for o ALVO do evento
            return context.Target?.Id == holder.Id;
        }

        // Lógica para triggers Ofensivos (Quem causa a ação)
        if (trigger == ModifierTrigger.ON_DEAL_MELEE_ATTACK ||
            trigger == ModifierTrigger.ON_DEAL_RANGED_ATTACK ||
            trigger == ModifierTrigger.ON_DEAL_MAGIC_DAMAGE)
        {
            // O trigger só dispara se o portador do modifier for a FONTE do evento
            return context.Source.Id == holder.Id;
        }

        // Triggers de Turno (Start/End) ou Passivos geralmente aplicam-se sempre ao próprio
        // ou dependem apenas de estar vivo, pelo que retornamos true por defeito.

        return true;
    }

    private void ExecuteTriggeredAbility(string abilityId, Combatant source, TriggerContext context)
    {
        // 1. Obter a definição da Habilidade
        if (!_abilityRepo.TryGetDefinition(abilityId, out var ability))
        {
            _logger.LogWarning("Triggered Ability {AbilityId} not found in repository.", abilityId);
            return;
        }

        _logger.LogInformation("Trigger executing ability {AbilityId} from source {Source}", abilityId, source.Name);

        // 2. Definir Alvos Automáticos
        // Por defeito, habilidades de reação (Counters) visam a FONTE do evento (o atacante).
        // Habilidades passivas (Heal Self) visam o SELF.
        // A lógica de targeting da AbilityDefinition decide qual regra usar.
        // Aqui construímos um input "falso" para o Auto-Targeting do CombatEngine resolver.
        var autoTargets = ResolveAutoTargets(context, ability, source);

        // 3. Executar (Sem custos, ou custos especiais se definidos)
        var payment = new Dictionary<EssenceType, int>();

        _combatEngine.Value.ExecuteAbility(
            context.GameState,
            ability,
            source,
            autoTargets,
            payment
        );
    }

    private AbilityTargets ResolveAutoTargets(TriggerContext context, AbilityDefinition ability, Combatant source)
    {
        var targets = new AbilityTargets();

        foreach (var rule in ability.TargetingRules)
        {
            // Lógica simplificada de Auto-Targeting para Triggers
            // Se a regra pede Inimigo, e o contexto tem um atacante (Source), usamos esse.
            if (rule.Type == TargetType.Enemy && context.Source.Id != source.Id)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { context.Source.Id };
            }
            // Se a regra pede Self/Friendly, e o contexto tem Target, usamos esse
            else if ((rule.Type == TargetType.Self || rule.Type == TargetType.Friendly) && context.Target != null)
            {
                targets.SelectedTargets[rule.RuleId] = new List<int> { source.Id };
            }
        }
        return targets;
    }
}