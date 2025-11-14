using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class DamageEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statService;
    private readonly ILogger<DamageEffectHandler> _logger;

    public DamageEffectHandler(IStatCalculationService statService, ILogger<DamageEffectHandler> logger)
    {
        _statService = statService;
        _logger = logger;
    }

    public EffectType SupportedType => EffectType.DAMAGE;

    /// <summary>
    /// Applies a DAMAGE effect by calculating scaled damage and applying it to the target.
    /// </summary>
    public void Apply(EffectDefinition def, Combatant source, Combatant target)
    {
        // --- 1. LÓGICA DE ATAQUE (Baseada no DeliveryMethod) ---
        float sourceStatValue = 0;

        switch (def.Delivery)
        {
            case DeliveryMethod.Melee:
                sourceStatValue = _statService.GetStatValue(source, StatType.Attack);
                break;
            case DeliveryMethod.Ranged:
                sourceStatValue = _statService.GetStatValue(source, StatType.Agility);
                break;
            case DeliveryMethod.Spell:
                sourceStatValue = _statService.GetStatValue(source, StatType.Magic);
                break;
            case DeliveryMethod.Passive:
                sourceStatValue = 0; // Dano passivo não escala (usa BaseAmount)
                break;
        }

        // 2. Calcular dano bruto
        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;

        // --- 3. LÓGICA DE DEFESA (Baseada no DamageType) ---
        float targetDefenseValue = 0;

        // CORREÇÃO DO BUG: Dano Passivo e Dano Real (True) ignoram a defesa.
        if (def.Delivery != DeliveryMethod.Passive && def.DamageType != DamageType.True)
        {
            switch (def.DamageType)
            {
                case DamageType.Physical:
                    targetDefenseValue = _statService.GetStatValue(target, StatType.Defense);
                    break;

                // ATUALIZAÇÃO: Agora todos usam a tua nova MagicDefense
                case DamageType.Magic:
                case DamageType.Mental:
                case DamageType.Holy:
                case DamageType.Dark:
                case DamageType.Nature:
                    targetDefenseValue = _statService.GetStatValue(target, StatType.MagicDefense);
                    break;
            }
        }

        // 4. Mitigação e aplicação
        float finalDamage = rawDamage - targetDefenseValue;
        if (finalDamage < 1) finalDamage = 1;

        int damageToApply = (int)finalDamage;

        _logger.LogInformation(
            "Applying {Damage} {DamageType} damage to {TargetName} (Raw: {RawDamage}, Def: {TargetDefense})",
            damageToApply, def.DamageType, target.Name, rawDamage, targetDefenseValue
        );

        target.CurrentHP -= damageToApply;
    }
}