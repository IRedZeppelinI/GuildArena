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

    public DamageResolutionResult ResolveDamage(
        float baseDamage,
        EffectDefinition effect,
        Combatant source,
        Combatant target)
    {
        var result = new DamageResolutionResult();

        // 1. Calcular Modificadores (Buffs/Resistências)       
        float modifiedDamage = ApplyModifiers(baseDamage, effect, source, target);

        // 2. Absorção por Barreiras
        if (modifiedDamage > 0)
        {
            // Construir tags do ataque (Tags da skill + Tipo de Dano)
            var attackTags = new HashSet<string>(effect.Tags);
            attackTags.Add(effect.DamageType.ToString()); //TODO rever implementação de tags/damageType

            ApplyBarriers(target, ref modifiedDamage, attackTags, result);
        }

        result.FinalDamageToApply = modifiedDamage;
        return result;
    }

    // --- Lógica de Modifiers ---
    private float ApplyModifiers(float damage, EffectDefinition effect, Combatant source, Combatant target)
    {
        float flatBonus = 0;
        float percentBonus = 0;
        float flatReduction = 0;
        float percentReduction = 0;

        var definitions = _modifierRepo.GetAllDefinitions();

        // Bónus do Atacante
        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var dmgMod in def.DamageModifications)
                {
                    if (dmgMod.Value > 0 && effect.Tags.Contains(dmgMod.RequiredTag))
                    {
                        if (dmgMod.Type == ModificationType.FLAT) flatBonus += dmgMod.Value;
                        else percentBonus += dmgMod.Value;
                    }
                }
            }
        }

        // Resistências do Alvo
        foreach (var mod in target.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var dmgMod in def.DamageModifications)
                {
                    // Resistências são valores negativos 
                    if (dmgMod.Value < 0 && effect.Tags.Contains(dmgMod.RequiredTag))
                    {
                        if (dmgMod.Type == ModificationType.FLAT) flatReduction += dmgMod.Value;
                        else percentReduction += dmgMod.Value;
                    }
                }
            }
        }

        float finalValue = (damage + flatBonus + flatReduction) * (1 + percentBonus + percentReduction);
        return Math.Max(0, finalValue);
    }

    // ---  Barreiras ---
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

            // Verificar Tags: Se BlockedTags estiver vazio, bloqueia tudo.
            bool blocks = modDef.Barrier.BlockedTags.Count == 0 ||
                          modDef.Barrier.BlockedTags.Any(t => attackTags.Contains(t));

            if (!blocks) continue;

            // Absorver
            float absorbed = Math.Min(currentDamage, mod.CurrentBarrierValue);

            mod.CurrentBarrierValue -= absorbed;
            currentDamage -= absorbed;
            result.AbsorbedDamage += absorbed;

            _logger.LogDebug("Barrier {Name} absorbed {Amount}. Remaining: {Val}",
                modDef.Name, absorbed, mod.CurrentBarrierValue);
        }
    }
}