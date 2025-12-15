using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <inheritdoc />
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
        bool isTrueDamage = effect.DamageCategory == DamageCategory.True;

        float modifiedDamage = ApplyModifiers(baseDamage, allAttackTags, source, target, isTrueDamage);

        // Barreiras são ignoradas por True Damage
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
        tags.Add(effect.DamageCategory.ToString());
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

        // -------------------------------------------------------------
        // 1. PROCESSAR BÓNUS DO ATACANTE (SOURCE)
        // Lógica: Bónus de Dano (Slayer, Buffs) aplicam-se SEMPRE,
        // mesmo que o dano base seja True Damage.
        // -------------------------------------------------------------
        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var dmgMod in def.DamageModifications)
                {
                    // Filtramos apenas Bónus (Valores Positivos) vindos do atacante
                    if (dmgMod.Value <= 0) continue;

                    // Regra de Tag
                    if (!tags.Contains(dmgMod.RequiredTag)) continue;

                    // Regra Racial (Slayer): Verifica se o ALVO corresponde à raça pedida
                    if (!string.IsNullOrEmpty(dmgMod.TargetRaceId))
                    {
                        if (target.RaceId != dmgMod.TargetRaceId) continue;
                    }

                    if (dmgMod.Type == ModificationType.FLAT)
                        flatBonus += dmgMod.Value;
                    else
                        percentBonus += dmgMod.Value;
                }
            }
        }

        // -------------------------------------------------------------
        // 2. PROCESSAR RESISTÊNCIAS DO ALVO (TARGET)
        // Lógica: Resistências só se aplicam se NÃO for True Damage.
        // True Damage ignora toda a mitigação do alvo.
        // -------------------------------------------------------------
        if (!isTrueDamage)
        {
            foreach (var mod in target.ActiveModifiers)
            {
                if (definitions.TryGetValue(mod.DefinitionId, out var def))
                {
                    foreach (var dmgMod in def.DamageModifications)
                    {
                        // Filtramos apenas Reduções (Valores Negativos) vindos do defensor
                        if (dmgMod.Value >= 0) continue;

                        // Regra de Tag
                        if (!tags.Contains(dmgMod.RequiredTag)) continue;

                        // Regra Racial: Verifica se o ATACANTE corresponde à raça pedida
                        // (Ex: Armadura que só protege contra Orcs)
                        if (!string.IsNullOrEmpty(dmgMod.TargetRaceId))
                        {
                            if (source.RaceId != dmgMod.TargetRaceId) continue;
                        }

                        if (dmgMod.Type == ModificationType.FLAT)
                            flatReduction += dmgMod.Value;
                        else
                            percentReduction += dmgMod.Value;
                    }
                }
            }
        }

        // Cálculo Final: (Base + Bónus - Redução) * (1 + %Bónus - %Redução)
        // Nota: Somamos tudo porque as reduções já vêm como valores negativos
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