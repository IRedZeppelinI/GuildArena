using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class DamageEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statService;
    private readonly ILogger<DamageEffectHandler> _logger;
    private readonly IDamageResolutionService _resolutionService;
    private readonly ITriggerProcessor _triggerProcessor;
    private readonly IBattleLogService _battleLog;

    public DamageEffectHandler(
        IStatCalculationService statService,
        ILogger<DamageEffectHandler> logger,
        IDamageResolutionService resolutionService,
        ITriggerProcessor triggerProcessor,
        IBattleLogService battleLog)
    {
        _statService = statService;
        _logger = logger;
        _resolutionService = resolutionService;
        _triggerProcessor = triggerProcessor;
        _battleLog = battleLog;
    }

    public EffectType SupportedType => EffectType.DAMAGE;

    public void Apply(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        GameState gameState,
        CombatActionResult actionResult)
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

            // --- BATTLE LOG (Para o Cliente) ---
            _battleLog.Log($"{target.Name} took {damageInt} damage.");

            // App Log (Para nós/Debug)
            _logger.LogInformation(
                "Hit {Target} for {Damage} (Absorbed: {Absorbed}). HP Left: {HP}",
                target.Name, damageInt, resolution.AbsorbedDamage, target.CurrentHP);

            // 4. Disparar Triggers
            // Nota: Na Fase 3 o triggerProcessor vai apenas agendar, mas por agora mantém a chamada.
            TriggerEvents(def, source, target, damageInt, gameState);
        }
        else
        {
            // Dano totalmente absorvido ou mitigado
            _battleLog.Log($"{target.Name} took no damage (Absorbed).");

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
            Tags = new HashSet<string>(def.Tags) { def.DamageCategory.ToString() }
        };

        // 1. Triggers Genéricos de Dano (Sempre disparam se houver HP loss)
        _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_DAMAGE, context);
        _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_DAMAGE, context);

        // 2. Triggers Específicos de Categoria (Physical vs Magic DAMAGE)
        if (def.DamageCategory == DamageCategory.Physical)
        {
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_PHYSICAL_DAMAGE, context);
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_PHYSICAL_DAMAGE, context);
        }
        else if (def.DamageCategory == DamageCategory.Magical)
        {
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_MAGIC_DAMAGE, context);
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_MAGIC_DAMAGE, context);
        }

        // 3. Triggers de DELIVERY (O ato de atacar - Melee, Ranged, Magic Attack)
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
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_SPELL_ATTACK, context);
                _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_SPELL_ATTACK, context);
                break;
        }
    }

    private float CalculateMitigatedDamage(EffectDefinition def, Combatant source, Combatant target)
    {
        float sourceStatValue = 0;
        if (def.ScalingStat != StatType.None)
        {
            sourceStatValue = _statService.GetStatValue(source, def.ScalingStat);
        }
        else
        {
            //TODO: Rever se quero este fallback ou obrigar effects a conterem sempre ScalingStat
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
            }
        }

        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;

        float targetDefenseValue = 0;

        if (def.DamageCategory != DamageCategory.True)
        {
            if (def.DamageCategory == DamageCategory.Physical)
            {
                targetDefenseValue = _statService.GetStatValue(target, StatType.Defense);
            }
            else if (def.DamageCategory == DamageCategory.Magical)
            {
                targetDefenseValue = _statService.GetStatValue(target, StatType.MagicDefense);
            }
        }

        return Math.Max(0, rawDamage - targetDefenseValue);
    }
}