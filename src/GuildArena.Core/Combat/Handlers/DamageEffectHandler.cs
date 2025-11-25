using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
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
    private readonly ITriggerProcessor _triggerProcessor;

    public DamageEffectHandler(
        IStatCalculationService statService,
        ILogger<DamageEffectHandler> logger,
        IDamageResolutionService resolutionService,
        ITriggerProcessor triggerProcessor)
    {
        _statService = statService;
        _logger = logger;
        _resolutionService = resolutionService;
        _triggerProcessor = triggerProcessor;
    }

    public EffectType SupportedType => EffectType.DAMAGE;

    public void Apply(EffectDefinition def, Combatant source, Combatant target, GameState gameState)
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

            // 4. Disparar Triggers (Ganchos)
            // Só disparamos se houve dano real (ou se quisermos incluir '0 damage hits', removemos o if)
            TriggerEvents(def, source, target, damageInt, gameState);
        }
        else
        {
            _logger.LogInformation("{Target} took no damage (Absorbed: {Absorbed}).",
                target.Name, resolution.AbsorbedDamage);
        }
    }


    private void TriggerEvents(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        float damageAmount,
        GameState gameState)
    {
        var context = new TriggerContext
        {
            Source = source,
            Target = target,
            GameState = gameState, 
            Value = damageAmount,
            Tags = new HashSet<string>(def.Tags) { def.DamageType.ToString() }
        };

        // Triggers Genéricos
        _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_DAMAGE, context);
        _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_DAMAGE, context);

        // Triggers Específicos por Delivery
        switch (def.Delivery)
        {
            case DeliveryMethod.Melee:
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_MELEE_ATTACK, context);
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MELEE_ATTACK, context);
                break;
            case DeliveryMethod.Ranged:
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_RANGED_ATTACK, context);
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_RANGED_ATTACK, context);
                break;
            case DeliveryMethod.Spell:
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_MAGIC_DAMAGE, context);
                break;
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
            if (def.DamageType == DamageType.Martial)
                targetDefenseValue = _statService.GetStatValue(target, StatType.Defense);
            else
                targetDefenseValue = _statService.GetStatValue(target, StatType.MagicDefense);
        }

        return rawDamage - targetDefenseValue;
    }
}