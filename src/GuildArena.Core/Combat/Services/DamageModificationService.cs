using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Services;

public class DamageModificationService : IDamageModificationService
{
    private readonly IReadOnlyDictionary<string, ModifierDefinition> _modifierDefinitions;

    public DamageModificationService(IModifierDefinitionRepository modifierRepository)
    {
        _modifierDefinitions = modifierRepository.GetAllDefinitions();
    }

    public float CalculateModifiedValue(float baseDamage, EffectDefinition effect, Combatant source, Combatant target)
    {
        float flatBonus = 0;
        float percentBonus = 0;
        float flatReduction = 0;
        float percentReduction = 0;

        // --- 1. MODIFIERS DE BÓNUS (Do Atacante) ---
        foreach (var activeMod in source.ActiveModifiers)
        {
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef)) continue;

            foreach (var dmgMod in modDef.DamageModifications)
            {
                if (effect.Tags.Contains(dmgMod.RequiredTag) && dmgMod.Value > 0)
                {
                    if (dmgMod.Type == ModificationType.FLAT)
                        flatBonus += dmgMod.Value;
                    else if (dmgMod.Type == ModificationType.PERCENTAGE)
                        percentBonus += dmgMod.Value;
                }
            }
        }

        // --- 2. MODIFIERS DE RESISTÊNCIA (Do Alvo) ---
        foreach (var activeMod in target.ActiveModifiers)
        {
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef)) continue;

            foreach (var dmgMod in modDef.DamageModifications)
            {
                if (effect.Tags.Contains(dmgMod.RequiredTag) && dmgMod.Value < 0)
                {
                    if (dmgMod.Type == ModificationType.FLAT)
                        flatReduction += dmgMod.Value;
                    else if (dmgMod.Type == ModificationType.PERCENTAGE)
                        percentReduction += dmgMod.Value;
                }
            }
        }

        // --- 3. CÁLCULO FINAL ---
        float finalValue = (baseDamage + flatBonus + flatReduction) * (1 + percentBonus + percentReduction);

        return finalValue;
    }
}