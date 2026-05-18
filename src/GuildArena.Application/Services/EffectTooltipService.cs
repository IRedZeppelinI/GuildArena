using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Application.Services;

public class EffectTooltipService : IEffectTooltipService
{
    private readonly IStatCalculationService _statService;
    private readonly IModifierDefinitionRepository _modifierRepo;

    public EffectTooltipService(
        IStatCalculationService statService,
        IModifierDefinitionRepository modifierRepo)
    {
        _statService = statService;
        _modifierRepo = modifierRepo;
    }

    public AbilityEffectSummaryDto GeneratePreview(Combatant source, EffectDefinition effectDef)
    {
        var dto = new AbilityEffectSummaryDto
        {
            Type = effectDef.Type,
            BaseAmount = effectDef.BaseAmount,
            ScalingStat = effectDef.ScalingStat,
            ScalingFactor = effectDef.ScalingFactor
        };

        // 1. MATEMÁTICA DE DANO E CURA
        if (effectDef.Type == EffectType.DAMAGE || effectDef.Type == EffectType.HEAL)
        {
            // Pede ao Motor de Combate (Core) o Stat atualizado do Herói!
            float statValue = _statService.GetStatValue(source, effectDef.ScalingStat);
            float rawOutput = effectDef.BaseAmount + (statValue * effectDef.ScalingFactor);

            if (effectDef.Type == EffectType.DAMAGE)
            {
                float flatBonus = 0;
                float percentBonus = 0;
                var allMods = _modifierRepo.GetAllDefinitions();

                var attackTags = new HashSet<string>(effectDef.Tags, StringComparer.OrdinalIgnoreCase)
                {
                    effectDef.DamageCategory.ToString()
                };

                foreach (var activeMod in source.ActiveModifiers)
                {
                    if (allMods.TryGetValue(activeMod.DefinitionId, out var def))
                    {
                        foreach (var dmgMod in def.DamageModifications)
                        {                            
                            if (dmgMod.Value > 0 &&
                                string.IsNullOrEmpty(dmgMod.TargetRaceId) &&
                                attackTags.Contains(dmgMod.RequiredTag))
                            {
                                if (dmgMod.Type == ModificationType.FLAT) flatBonus += dmgMod.Value;
                                else percentBonus += dmgMod.Value;
                            }
                        }
                    }
                }
                rawOutput = (rawOutput + flatBonus) * (1 + percentBonus);
            }

            dto.PredictedValue = (int)Math.Round(Math.Max(0, rawOutput));
        }

        // 2. EXTRAÇÃO DE TEXTO PARA MODIFICADORES
        else if (effectDef.Type == EffectType.APPLY_MODIFIER && !string.IsNullOrEmpty(effectDef.ModifierDefinitionId))
        {
            var allMods = _modifierRepo.GetAllDefinitions();
            if (allMods.TryGetValue(effectDef.ModifierDefinitionId, out var modDef))
            {
                dto.ModifierName = modDef.Name;
                dto.ModifierDescription = modDef.Description;
            }
        }

        return dto;
    }
}