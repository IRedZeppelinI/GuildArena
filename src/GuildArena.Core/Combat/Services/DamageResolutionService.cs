using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Implements the logic for damage resolution, including tag-based modifiers and barrier absorption.
/// </summary>
public class DamageResolutionService : IDamageResolutionService
{
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly ILogger<DamageResolutionService> _logger;

    public DamageResolutionService(
        IModifierDefinitionRepository modifierRepo,
        ILogger<DamageResolutionService> logger)
    {
        _modifierRepo = modifierRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public DamageResolutionResult ResolveDamage(
        float baseDamage,
        EffectDefinition effect,
        Combatant source,
        Combatant target)
    {
        var result = new DamageResolutionResult();

        var allAttackTags = BuildAggregatedTags(effect);
        bool isTrueDamage = effect.DamageType == DamageType.True;

        float modifiedDamage = ApplyModifiers(baseDamage, allAttackTags, source, target, isTrueDamage);

        if (modifiedDamage > 0 && !isTrueDamage)
        {
            ApplyBarriers(target, ref modifiedDamage, allAttackTags, result);
        }

        result.FinalDamageToApply = modifiedDamage;
        return result;
    }

    private HashSet<string> BuildAggregatedTags(EffectDefinition effect)
    {
        var tags = new HashSet<string>(effect.Tags, StringComparer.OrdinalIgnoreCase);
        tags.Add(effect.DamageType.ToString());
        return tags;
    }

    private float ApplyModifiers(
        float damage,
        HashSet<string> tags,
        Combatant source,
        Combatant target,
        bool isTrueDamage)
    {
        float flatBonus = 0;
        float percentBonus = 0;
        float flatReduction = 0;
        float percentReduction = 0;

        var definitions = _modifierRepo.GetAllDefinitions();

        // Processar Bónus do Atacante
        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var dmgMod in def.DamageModifications)
                {
                    if (dmgMod.Value > 0 && tags.Contains(dmgMod.RequiredTag))
                    {
                        if (dmgMod.Type == ModificationType.FLAT)
                            flatBonus += dmgMod.Value;
                        else
                            percentBonus += dmgMod.Value;
                    }
                }
            }
        }

        // Processar Resistências do Alvo
        if (!isTrueDamage)
        {
            foreach (var mod in target.ActiveModifiers)
            {
                if (definitions.TryGetValue(mod.DefinitionId, out var def))
                {
                    foreach (var dmgMod in def.DamageModifications)
                    {
                        if (dmgMod.Value < 0 && tags.Contains(dmgMod.RequiredTag))
                        {
                            if (dmgMod.Type == ModificationType.FLAT)
                                flatReduction += dmgMod.Value;
                            else
                                percentReduction += dmgMod.Value;
                        }
                    }
                }
            }
        }

        float finalValue = (damage + flatBonus + flatReduction) * (1 + percentBonus + percentReduction);
        return Math.Max(0, finalValue);
    }

    private void ApplyBarriers(
        Combatant target,
        ref float currentDamage,
        HashSet<string> attackTags,
        DamageResolutionResult result)
    {
        var definitions = _modifierRepo.GetAllDefinitions();

        foreach (var mod in target.ActiveModifiers)
        {
            if (currentDamage <= 0) break;
            if (mod.CurrentBarrierValue <= 0) continue;

            if (!definitions.TryGetValue(mod.DefinitionId, out var modDef)) continue;
            if (modDef.Barrier == null) continue;

            bool blocks = modDef.Barrier.BlockedTags.Count == 0 ||
                          modDef.Barrier.BlockedTags.Any(t => attackTags.Contains(t));

            if (!blocks) continue;

            float absorbed = Math.Min(currentDamage, mod.CurrentBarrierValue);

            mod.CurrentBarrierValue -= absorbed;
            currentDamage -= absorbed;
            result.AbsorbedDamage += absorbed;

            _logger.LogDebug("Barrier {Name} absorbed {Amount}. Remaining: {Val}",
                modDef.Name, absorbed, mod.CurrentBarrierValue);
        }
    }
}