using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories; 
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class DamageEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statCalculationService;
    private readonly ILogger<DamageEffectHandler> _logger;

    // cache de modifiers defimitions
    private readonly IReadOnlyDictionary<string, ModifierDefinition> _modifierDefinitions;

    
    public DamageEffectHandler(
        IStatCalculationService statService,
        ILogger<DamageEffectHandler> logger,
        IModifierDefinitionRepository modifierRepository) 
    {
        _statCalculationService = statService;
        _logger = logger;

        // Cria a cache O(1) quando o handler é instanciado
        _modifierDefinitions = modifierRepository.GetAllDefinitions();
    }

    public EffectType SupportedType => EffectType.DAMAGE;

    /// <summary>
    /// Applies a DAMAGE effect by calculating scaled damage, applying modifiers/resistances, and applying it to the target.
    /// </summary>
    public void Apply(EffectDefinition def, Combatant source, Combatant target)
    {
        // --- 1. CÁLCULO DE DANO BASE (Mitigado) ---
        float sourceStatValue = 0;
        switch (def.Delivery)
        {
            case DeliveryMethod.Melee:
                sourceStatValue = _statCalculationService.GetStatValue(source, StatType.Attack);
                break;
            case DeliveryMethod.Ranged:
                sourceStatValue = _statCalculationService.GetStatValue(source, StatType.Agility);
                break;
            case DeliveryMethod.Spell:
                sourceStatValue = _statCalculationService.GetStatValue(source, StatType.Magic);
                break;
            case DeliveryMethod.Passive:
                sourceStatValue = 0;
                break;
        }

        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;
        float targetDefenseValue = 0;

        if (def.Delivery != DeliveryMethod.Passive && def.DamageType != DamageType.True)
        {
            switch (def.DamageType)
            {
                case DamageType.Physical:
                    targetDefenseValue = _statCalculationService.GetStatValue(target, StatType.Defense);
                    break;
                case DamageType.Magic:
                case DamageType.Mental:
                case DamageType.Holy:
                case DamageType.Dark:
                case DamageType.Nature:
                    targetDefenseValue = _statCalculationService.GetStatValue(target, StatType.MagicDefense);
                    break;
            }
        }

        float mitigatedDamage = rawDamage - targetDefenseValue;

        // --- 2. MODIFIERS DE BÓNUS (Do Atacante) ---
        float flatBonus = 0;
        float percentBonus = 0; // (ex: 0.20 para +20%)

        foreach (var activeMod in source.ActiveModifiers)
        {
            // CORREÇÃO: Usar o Dicionário, não o GetById()
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef)) continue;

            foreach (var dmgMod in modDef.DamageModifications)
            {
                // Este buff (+DMG) aplica-se a esta habilidade?
                if (def.Tags.Contains(dmgMod.RequiredTag) && dmgMod.Value > 0)
                {
                    if (dmgMod.Type == ModificationType.FLAT)
                        flatBonus += dmgMod.Value;
                    else if (dmgMod.Type == ModificationType.PERCENTAGE)
                        percentBonus += dmgMod.Value;
                }
            }
        }

        // --- 3. MODIFIERS DE RESISTÊNCIA (Do Alvo) ---
        // (A lógica que faltava)
        float flatReduction = 0;
        float percentReduction = 0; // (ex: -0.20 para 20% res)

        foreach (var activeMod in target.ActiveModifiers)
        {
            if (!_modifierDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef)) continue;

            // Reutilizamos a *mesma* lista de DamageModifications
            // Resistências são apenas valores negativos.
            foreach (var dmgMod in modDef.DamageModifications)
            {
                // Esta resistência (-DMG) aplica-se a esta habilidade?
                if (def.Tags.Contains(dmgMod.RequiredTag) && dmgMod.Value < 0)
                {
                    if (dmgMod.Type == ModificationType.FLAT)
                        flatReduction += dmgMod.Value; // (Soma um valor negativo)
                    else if (dmgMod.Type == ModificationType.PERCENTAGE)
                        percentReduction += dmgMod.Value; // (Soma -0.20)
                }
            }
        }

        // --- 4. CÁLCULO FINAL ---
        // Fórmula: (Dano_Mitigado + Bónus_Flat) * (1 + Bónus_% + Redução_%)
        // (Onde Redução_% é negativo)
        float finalDamage = (mitigatedDamage + flatBonus + flatReduction) * (1 + percentBonus + percentReduction);

        if (finalDamage < 1) finalDamage = 1;

        int damageToApply = (int)finalDamage;

        _logger.LogInformation(
            "Applying {Damage} {DamageType} damage to {TargetName} (Base: {MitigatedDamage}, Final: {FinalDamage})",
            damageToApply, def.DamageType, target.Name, mitigatedDamage, finalDamage
        );

        target.CurrentHP -= damageToApply;
    }
}