using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class DamageEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statCalculationService;
    private readonly ILogger<DamageEffectHandler> _logger;
    private readonly IDamageModificationService _damageModService;

    public DamageEffectHandler(
        IStatCalculationService statCalculationService, 
        ILogger<DamageEffectHandler> logger,
        IDamageModificationService damageModificationService)
    {
        _statCalculationService = statCalculationService; 
        _logger = logger;
        _damageModService = damageModificationService;
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
                sourceStatValue = 0; // Dano passivo não escala
                break;
        }

        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;
        float targetDefenseValue = 0;

        // CORREÇÃO: Preencher a lógica de defesa que faltava
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

                    // (O DamageType.True é ignorado pelo 'if' exterior)
            }
        }

        float mitigatedDamage = rawDamage - targetDefenseValue;

        // --- 2. CHAMAR O ESPECIALISTA DE MODIFIERS ---
        // Delegamos a lógica de Tags (Bónus/Resistências)
        float finalDamage = _damageModService.CalculateModifiedValue(
            mitigatedDamage,
            def,
            source,
            target
        );

        // --- 3. APLICAÇÃO ---
        if (finalDamage < 1) finalDamage = 1;
        int damageToApply = (int)finalDamage;

        _logger.LogInformation(
            "Applying {Damage} {DamageType} damage to {TargetName} (Base: {MitigatedDamage}, Final: {FinalDamage})",
            damageToApply, def.DamageType, target.Name, mitigatedDamage, finalDamage
        );

        target.CurrentHP -= damageToApply;
    }
}