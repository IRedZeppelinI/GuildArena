using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

/// <summary>
/// Handles the application of modifiers to a combatant.
/// </summary>
public class ApplyModifierHandler : IEffectHandler
{
    private readonly ILogger<ApplyModifierHandler> _logger;

    public ApplyModifierHandler(ILogger<ApplyModifierHandler> logger)
    {
        _logger = logger;
    }

    public EffectType SupportedType => EffectType.APPLY_MODIFIER;

    /// <summary>
    /// Applies a modifier to the target combatant's list of active modifiers.
    /// </summary>
    public void Apply(EffectDefinition def, Combatant source, Combatant target)
    {
        if (string.IsNullOrEmpty(def.ModifierDefinitionId))
        {
            _logger.LogWarning(
                "ApplyModifierHandler: EffectDefinition {EffectType} missing ModifierDefinitionId.",
                def.Type);
            return;
        }

        
        // Filtro para implementar Stacking se necessario
        var existingModifier = target.ActiveModifiers
            .FirstOrDefault(m => m.DefinitionId == def.ModifierDefinitionId);

        //se combatant já tem o modifier, não está a fazer stack
        if (existingModifier != null)
        {            
            existingModifier.TurnsRemaining = def.DurationInTurns; //faz refresh do que falta

            _logger.LogInformation(
                "Refreshed modifier {ModifierId} on {TargetName} for {Duration} turns.",
                def.ModifierDefinitionId, target.Name, def.DurationInTurns
            );
        }
        else // O modifier não existe
        {            
            var newActiveModifier = new ActiveModifier
            {
                DefinitionId = def.ModifierDefinitionId,
                TurnsRemaining = def.DurationInTurns,
                CasterId = source.Id
            };
            
            target.ActiveModifiers.Add(newActiveModifier);
            
            _logger.LogInformation(
                "Applied modifier {ModifierId} to {TargetName} for {Duration} turns.",
                newActiveModifier.DefinitionId, target.Name, newActiveModifier.TurnsRemaining
            );
        }
    }
}