using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class ApplyModifierHandler : IEffectHandler
{
    private readonly ILogger<ApplyModifierHandler> _logger;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IStatCalculationService _statService;

    public ApplyModifierHandler(
        ILogger<ApplyModifierHandler> logger,
        IModifierDefinitionRepository modifierRepo,
        IStatCalculationService statService)
    {
        _logger = logger;
        _modifierRepo = modifierRepo;
        _statService = statService;
    }

    public EffectType SupportedType => EffectType.APPLY_MODIFIER;

    public void Apply(EffectDefinition def, Combatant source, Combatant target, GameState gameState)
    {
        if (string.IsNullOrEmpty(def.ModifierDefinitionId))
        {
            _logger.LogWarning("ApplyModifierHandler: Missing ModifierDefinitionId.");
            return;
        }

        var definitions = _modifierRepo.GetAllDefinitions();
        if (!definitions.TryGetValue(def.ModifierDefinitionId, out var modDef))
        {
            _logger.LogWarning("Modifier Definition {Id} not found.", def.ModifierDefinitionId);
            return;
        }

        // Calcular valor da Barreira (se houver)
        float initialBarrierValue = 0;
        if (modDef.Barrier != null)
        {
            initialBarrierValue = CalculateBarrierValue(source, modDef.Barrier);

            // Aplica Bónus de Barreiras (Modificadores do Caster)
            initialBarrierValue = ApplyBarrierBonuses(source, initialBarrierValue, definitions);
        }

        var existingModifier = target.ActiveModifiers
            .FirstOrDefault(m => m.DefinitionId == def.ModifierDefinitionId);

        if (existingModifier != null)
        {
            existingModifier.TurnsRemaining = def.DurationInTurns;

            // Refresh: Restaura barreira se aplicável
            if (initialBarrierValue > 0)
            {
                existingModifier.CurrentBarrierValue = initialBarrierValue;
            }

            existingModifier.ActiveStatusEffects = modDef.GrantedStatusEffects.ToList();
            _logger.LogInformation("Refreshed modifier {ModifierId}.", def.ModifierDefinitionId);
        }
        else
        {
            var newActiveModifier = new ActiveModifier
            {
                DefinitionId = def.ModifierDefinitionId,
                TurnsRemaining = def.DurationInTurns,
                CasterId = source.Id,
                CurrentBarrierValue = initialBarrierValue,
                ActiveStatusEffects = modDef.GrantedStatusEffects 
            };

            target.ActiveModifiers.Add(newActiveModifier);
            _logger.LogInformation("Applied modifier {ModifierId}. Barrier: {Val}", newActiveModifier.DefinitionId, initialBarrierValue);
        }
    }

    private float CalculateBarrierValue(Combatant source, BarrierProperties barrierDef)
    {
        float statVal = 0;
        // Se houver scaling, vai buscar o stat
        if (barrierDef.ScalingFactor > 0)
        {
            statVal = _statService.GetStatValue(source, barrierDef.ScalingStat);
        }
        return (statVal * barrierDef.ScalingFactor) + barrierDef.BaseAmount;
    }

    private float ApplyBarrierBonuses(Combatant source, float baseValue, IReadOnlyDictionary<string, ModifierDefinition> definitions)
    {
        float flatBonus = 0;
        float percentBonus = 0;

        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var barMod in def.BarrierModifications)
                {
                    if (barMod.Type == ModificationType.FLAT) flatBonus += barMod.Value;
                    else percentBonus += barMod.Value;
                }
            }
        }
        return (baseValue + flatBonus) * (1 + percentBonus);
    }
}