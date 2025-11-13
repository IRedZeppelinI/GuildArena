using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Services;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat;

/// <summary>
/// Implements the core combat orchestration.
/// </summary>
public class CombatEngine : ICombatEngine
{
    // 2. Rigoroso: Usa um Dicionário para lookup O(1) (performance)
    private readonly IReadOnlyDictionary<EffectType, IEffectHandler> _handlers;

    public CombatEngine(IEnumerable<IEffectHandler> handlers)
    {
        // O construtor (via DI) recebe *todos* os handlers e organiza-os
        // num dicionário para acesso instantâneo.
        _handlers = handlers.ToDictionary(h => h.SupportedType, h => h);
    }

    /// <summary>
    /// Executes a given ability by orchestrating the required effect handlers.
    /// </summary>
    public void ExecuteAbility(AbilityDefinition ability, Combatant source, Combatant target)
    {
        foreach (var effect in ability.Effects)
        {
            // 3. Rigoroso: Acesso O(1)
            if (_handlers.TryGetValue(effect.Type, out var handler))
            {
                handler.Apply(effect, source, target);
            }
            else
            {
                // (Opcional, mas recomendado: Logar um aviso)
                // Log.Warning($"Nenhum IEffectHandler registado para o tipo {effect.Type}");
            }
        }
    }
}