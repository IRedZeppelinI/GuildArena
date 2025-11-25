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
    // Usamos Lazy para evitar Dependência Circular, pois o CombatEngine depende do TriggerProcessor
    private readonly Lazy<ICombatEngine> _combatEngine;
    private readonly ILogger<TriggerProcessor> _logger;

    public TriggerProcessor(
        IModifierDefinitionRepository modifierRepo,
        Lazy<ICombatEngine> combatEngine,
        ILogger<TriggerProcessor> logger)
    {
        _modifierRepo = modifierRepo;
        _combatEngine = combatEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public void ProcessTriggers(ModifierTrigger trigger, TriggerContext context)
    {
        var allDefinitions = _modifierRepo.GetAllDefinitions();

        // Iteramos sobre TODOS os combatentes para suportar triggers globais 
        // (ex: "Sempre que alguém morre", ou auras globais).
        foreach (var combatant in context.GameState.Combatants)
        {
            // Se o combatente estiver morto, só processamos triggers de morte (ex: Ressurreição)
            if (!combatant.IsAlive && trigger != ModifierTrigger.ON_DEATH) continue;

            // Iteramos ao contrário por segurança caso a lista seja modificada durante a execução
            // (embora neste contexto de leitura seja raro haver remoção direta aqui).
            foreach (var activeMod in combatant.ActiveModifiers)
            {
                if (!allDefinitions.TryGetValue(activeMod.DefinitionId, out var def)) continue;

                // 1. Verificação Primária: O modifier subscreve este trigger?
                if (!def.Triggers.Contains(trigger)) continue;

                // 2. Verificação Contextual: O portador do modifier é relevante para o evento?
                if (!ValidateCondition(trigger, combatant, context)) continue;

                // 3. Execução: Dispara a Habilidade Interna associada
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
        _logger.LogInformation("Trigger executing ability {AbilityId} from source {Source}", abilityId, source.Name);

        // TODO: Aqui necessitaremos de injetar o IAbilityDefinitionRepository para obter a definição real.
        // Como ainda não temos esse repositório no contexto, simulo a obtenção da AbilityDefinition.

        // AbilityDefinition ability = _abilityRepo.GetById(abilityId);

        // Exemplo de execução:
        // As habilidades de trigger são geralmente gratuitas (Custo zero) e automáticas.
        // O alvo depende da lógica da habilidade (ex: Thorns ataca o context.Source).

        // var payment = new Dictionary<EssenceType, int>(); 
        // var autoTargets = ResolveAutoTargets(context, ability);

        // _combatEngine.Value.ExecuteAbility(
        //     context.GameState, 
        //     ability, 
        //     source, 
        //     autoTargets, 
        //     payment
        // );
    }
}