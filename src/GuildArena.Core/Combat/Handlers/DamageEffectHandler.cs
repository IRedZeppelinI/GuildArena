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
    private readonly IDamageResolutionService _resolutionService;

    public DamageEffectHandler(
        IStatCalculationService statService,
        ILogger<DamageEffectHandler> logger,
        IDamageResolutionService resolutionService)
    {
        _statService = statService;
        _logger = logger;
        _resolutionService = resolutionService;
    }

    public EffectType SupportedType => EffectType.DAMAGE;

    public void Apply(EffectDefinition def, Combatant source, Combatant target)
    {
        // 1. Calcular Dano Bruto Mitigado (Stats vs Defesa)
        float mitigatedDamage = CalculateMitigatedDamage(def, source, target);

        // 2. Resolver Dano Final (Modifiers + Barreiras)
        var resolution = _resolutionService.ResolveDamage(mitigatedDamage, def, source, target);

        // 3. Aplicar ao HP
        if (resolution.FinalDamageToApply > 0)
        {
            int damageInt = (int)resolution.FinalDamageToApply;
            target.CurrentHP -= damageInt;

            _logger.LogInformation(
                "Hit {Target} for {Damage} (Absorbed: {Absorbed}). HP Left: {HP}",
                target.Name, damageInt, resolution.AbsorbedDamage, target.CurrentHP);
        }
        else
        {
            _logger.LogInformation("{Target} took no damage (Absorbed: {Absorbed}).",
                target.Name, resolution.AbsorbedDamage);
        }
    }

    private float CalculateMitigatedDamage(EffectDefinition def, Combatant source, Combatant target)
    {
        float sourceStatValue = 0;
        switch (def.Delivery)
        {
            case DeliveryMethod.Melee: sourceStatValue = _statService.GetStatValue(source, StatType.Attack); break;
            case DeliveryMethod.Ranged: sourceStatValue = _statService.GetStatValue(source, StatType.Agility); break;
            case DeliveryMethod.Spell: sourceStatValue = _statService.GetStatValue(source, StatType.Magic); break;
            case DeliveryMethod.Passive: sourceStatValue = 0; break;
        }

        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;
        float targetDefenseValue = 0;

        if (def.Delivery != DeliveryMethod.Passive && def.DamageType != DamageType.True)
        {
            // Nota: Usamos os novos Enums (Martial vs Outros)
            if (def.DamageType == DamageType.Martial)
                targetDefenseValue = _statService.GetStatValue(target, StatType.Defense);
            else
                targetDefenseValue = _statService.GetStatValue(target, StatType.MagicDefense);
        }

        return rawDamage - targetDefenseValue;
    }
}