using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
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
    public void ExecuteAbility(AbilityDefinition ability, Combatant source, Combatant target)
    {
        _logger.LogInformation(
            "Executing ability {AbilityId} from {SourceId} on {TargetId}",
            ability.Id, source.Id, target.Id
        );

        foreach (var effect in ability.Effects)
        {
            if (_handlers.TryGetValue(effect.Type, out var handler))
            {
                _logger.LogDebug("Applying effect type {EffectType} via {HandlerName}",
                    effect.Type, handler.GetType().Name);

                handler.Apply(effect, source, target);
            }
            else
            {
                _logger.LogWarning(
                    "No IEffectHandler found for EffectType {EffectType} in Ability {AbilityId}.",
                    effect.Type, ability.Id
                );
            }
        }
    }
}