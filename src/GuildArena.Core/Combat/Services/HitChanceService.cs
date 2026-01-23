using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;

namespace GuildArena.Core.Combat.Services;

public class HitChanceService : IHitChanceService
{
    private readonly IStatCalculationService _statService;
    private readonly IModifierDefinitionRepository _modifierRepo; 

    private const float BaseChance = 1.0f;
    private const float OffenseFactor = 0.005f;
    private const float DefenseFactor = 0.01f;
    private const float LevelDeltaFactor = 0.02f;
    private const float MinChance = 0.05f;
    private const float MaxChance = 1.0f;

    public HitChanceService(
        IStatCalculationService statService,
        IModifierDefinitionRepository modifierRepo) 
    {
        _statService = statService;
        _modifierRepo = modifierRepo;
    }

    public float CalculateHitChance(Combatant source, Combatant target, EffectDefinition effect)
    {
        // 1. Efeitos inevitáveis (Buffs, Curas, True Damage se configurado assim)
        if (!effect.CanBeEvaded)
        {
            return 1.0f;
        }

        if (effect.ConditionGuaranteedHit && effect.ConditionStatus.HasValue)
        {
            if (HasStatus(target, effect.ConditionStatus.Value))
            {
                return 1.0f; // Acerto garantido
            }
        }


        // 2. Cálculo Base (Stats) - Lógica existente
        float sourceStatValue = 0;
        float targetStatValue = 0;

        switch (effect.Delivery)
        {
            case DeliveryMethod.Melee:
                sourceStatValue = _statService.GetStatValue(source, StatType.Attack);
                targetStatValue = _statService.GetStatValue(target, StatType.Agility);
                break;

            case DeliveryMethod.Ranged:
                sourceStatValue = _statService.GetStatValue(source, StatType.Agility);
                targetStatValue = _statService.GetStatValue(target, StatType.Agility);
                break;

            case DeliveryMethod.Spell:
                sourceStatValue = _statService.GetStatValue(source, StatType.Magic);
                targetStatValue = _statService.GetStatValue(target, StatType.MagicDefense);
                break;

            default:
                return 1.0f;
        }

        int levelDiff = source.Level - target.Level;
        float levelCorrection = levelDiff * LevelDeltaFactor;

        float baseChanceCalculation = BaseChance
                       + (sourceStatValue * OffenseFactor)
                       - (targetStatValue * DefenseFactor)
                       + levelCorrection;

        // 3. Modificadores de Precisão (Attacker Hit Bonus/Penalty)
        // Ex: Blind (-0.20), Eagle Eye (+0.10)
        float hitModsValue = GetHitChanceModifiers(source, effect);

        // 4. Modificadores de Evasão (Defender Evasion Bonus)
        // Ex: Blur (+0.15)
        float evasionModsValue = GetEvasionModifiers(target, effect);

        // Fórmula Final: Base + (Bonus Hit) - (Bonus Evasion)
        float finalChance = baseChanceCalculation + hitModsValue - evasionModsValue;

        // 5. Clamping
        return Math.Clamp(finalChance, MinChance, MaxChance);
    }

    private float GetHitChanceModifiers(Combatant source, EffectDefinition effect)
    {
        float total = 0f;
        var definitions = _modifierRepo.GetAllDefinitions();

        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var hitMod in def.HitChanceModifications)
                {
                    // Filtro por Tag (ex: +10% Hit apenas em "Ranged")
                    if (!string.IsNullOrEmpty(hitMod.RequiredAbilityTag) &&
                        !effect.Tags.Contains(hitMod.RequiredAbilityTag, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    total += hitMod.Value;
                }
            }
        }
        return total;
    }

    private float GetEvasionModifiers(Combatant target, EffectDefinition effect)
    {
        float total = 0f;
        var definitions = _modifierRepo.GetAllDefinitions();

        foreach (var mod in target.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var evaMod in def.EvasionModifications)
                {
                    // Filtro por Categoria (ex: Blur dá Evasão apenas contra "Physical")
                    if (!string.IsNullOrEmpty(evaMod.RequiredDamageCategory))
                    {
                        bool categoryMatch = effect.DamageCategory.ToString()
                            .Equals(evaMod.RequiredDamageCategory, StringComparison.OrdinalIgnoreCase);

                        // Também verificamos Tags para flexibilidade (ex: Evade "Fire")
                        bool tagMatch = effect.Tags.Contains(evaMod.RequiredDamageCategory, StringComparer.OrdinalIgnoreCase);

                        if (!categoryMatch && !tagMatch) continue;
                    }
                    total += evaMod.Value;
                }
            }
        }
        return total;
    }

    private bool HasStatus(Combatant target, StatusEffectType status)
    {
        return target.ActiveModifiers.Any(m => m.ActiveStatusEffects.Contains(status));
    }
}