using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat;

/// <summary>
/// Implements the core combat orchestration.
/// </summary>
public class CombatEngine : ICombatEngine
{
    // 2. Rigoroso: Usa um Dicionário para lookup O(1) (performance)
    private readonly IReadOnlyDictionary<EffectType, IEffectHandler> _handlers;
    private readonly ILogger<CombatEngine> _logger;

    public CombatEngine(IEnumerable<IEffectHandler> handlers, ILogger<CombatEngine> logger)
    {
        // O construtor (via DI) recebe *todos* os handlers e organiza-os
        // num dicionário para acesso instantâneo.
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
        _logger = logger;
    }

    /// <summary>
    /// Executes a given ability by orchestrating the required effect handlers.
    /// </summary>
    public void ExecuteAbility(
        GameState currentState,
        AbilityDefinition ability,
        Combatant source,
        AbilityTargets targets 
    )
    {
        _logger.LogInformation("Executing ability {AbilityId} from {SourceId}",
            ability.Id, source.Id);

        // (Lógica de Cooldown e Custo de Essence/HP viria aqui)

        foreach (var effect in ability.Effects)
        {
            if (!_handlers.TryGetValue(effect.Type, out var handler))
            {
                _logger.LogWarning("No IEffectHandler found for {EffectType}", effect.Type);
                continue;
            }

            
            var rule = ability.TargetingRules
                .FirstOrDefault(r => r.RuleId == effect.TargetRuleId);

            if (rule == null)
            {
                _logger.LogWarning("No TargetRuleId '{RuleId}' found in Ability {AbilityId}",
                    effect.TargetRuleId, ability.Id);
                continue;
            }

            
            var finalTargets = GetTargetsForRule(
                rule,
                source,
                currentState,
                targets 
            );

            
            foreach (var target in finalTargets)
            {
                handler.Apply(effect, source, target);
            }
        }
    }


   

    private List<Combatant> GetTargetsForRule(
        TargetingRule rule,
        Combatant source,
        GameState currentState,
        AbilityTargets abilityTargets)
    {
        // Obter a lista base de alvos 
        List<Combatant> baseTargetList;

        switch (rule.Type)
        {
            // Casos de "Clique" (usam o 'mapa' da UI)
            case TargetType.Enemy:
            case TargetType.Ally:
            case TargetType.Friendly:
                {
                    if (!abilityTargets.SelectedTargets.TryGetValue(rule.RuleId, out var selectedTargetIds))
                    {
                        _logger.LogWarning("No targets provided by UI for TargetRuleId '{RuleId}'", rule.RuleId);
                        return new List<Combatant>();
                    }
                    baseTargetList = currentState.Combatants
                        .Where(c => selectedTargetIds.Contains(c.Id))
                        .ToList();
                    break;
                }

            // Casos de "Não Clique" (AoE / Self)
            case TargetType.Self:
                baseTargetList = new List<Combatant> { source };
                break;
            case TargetType.AllEnemies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId != source.OwnerId)
                    .ToList();
                break;
            case TargetType.AllAllies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId && c.Id != source.Id)
                    .ToList();
                break;

            case TargetType.AllFriendlies:
                baseTargetList = currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId)
                    .ToList();
                break;
            default:
                baseTargetList = new List<Combatant>();
                break;
        }

        // Aplicar Filtros de Validação 
        List<Combatant> validatedTargets = new();

        // Filtro de "Vivo/Morto"
        if (!rule.CanTargetDead)
        {            
            validatedTargets = baseTargetList.Where(c => c.IsAlive).ToList();
        }
        else
        {
            // Regra de "Reviver": Só pode atingir alvos mortos.            
            validatedTargets = baseTargetList.Where(c => !c.IsAlive).ToList();
        }

        // Filtro de Tipo (Inimigo/Aliado) 
        switch (rule.Type)
        {
            case TargetType.Enemy:
            case TargetType.AllEnemies:
                return validatedTargets
                    .Where(t => t.OwnerId != source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Ally:
            case TargetType.AllAllies:
                return validatedTargets
                    .Where(t => t.OwnerId == source.OwnerId && t.Id != source.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Friendly:
            case TargetType.AllFriendlies:
                return validatedTargets
                    .Where(t => t.OwnerId == source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Self:
                return validatedTargets; // Já é 'source', não precisa de mais filtros

            default:
                return new List<Combatant>();
        }
    }
}