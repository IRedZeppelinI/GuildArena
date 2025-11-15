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
        List<int> selectedTargetIds)
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

            //cada efeito só tem um targetingRule, uma habilidade é que pode ter várias.
            var rule = ability.TargetingRules
                .FirstOrDefault(r => r.RuleId == effect.TargetRuleId);
            if (rule == null)
            {
                _logger.LogWarning("No TargetRuleId '{RuleId}' found in Ability {AbilityId}",
                    effect.TargetRuleId, ability.Id);
                continue;
            }

            // O "coração" da lógica: Encontrar os alvos
            var finalTargets = GetTargetsForRule(
                rule,
                source,
                currentState,
                selectedTargetIds
            );

            // Aplicar o efeito a todos os alvos (para AoE)
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
        List<int> selectedTargetIds)
    {
        // 1. Resolver os alvos selecionados pela UI (transformar IDs em Combatants)
        var selected = currentState.Combatants
            .Where(c => selectedTargetIds.Contains(c.Id))
            .ToList();

        // 2. Filtrar com base na regra
        switch (rule.Type)
        {
            // --- Casos Simples (Não precisam da lista 'selected') ---
            case TargetType.Self:
                return new List<Combatant> { source };

            case TargetType.AllEnemies:
                return currentState.Combatants
                    .Where(c => c.OwnerId != source.OwnerId)
                    .ToList();

            case TargetType.AllAllies:
                return currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId && c.Id != source.Id)
                    .ToList();

            case TargetType.AllFriendlies:
                return currentState.Combatants
                    .Where(c => c.OwnerId == source.OwnerId)
                    .ToList();

            // --- Casos que dependem do clique (usam a lista 'selected') ---
            case TargetType.Enemy:
                return selected
                    .Where(t => t.OwnerId != source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Ally:
                return selected
                    .Where(t => t.OwnerId == source.OwnerId && t.Id != source.Id)
                    .Take(rule.Count)
                    .ToList();

            case TargetType.Friendly:
                return selected
                    .Where(t => t.OwnerId == source.OwnerId)
                    .Take(rule.Count)
                    .ToList();

            default:
                return new List<Combatant>();
        }
    }
}