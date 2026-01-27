using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class HealEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statService;
    private readonly ITriggerProcessor _triggerProcessor;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<HealEffectHandler> _logger;

    public HealEffectHandler(
        IStatCalculationService statService,
        ITriggerProcessor triggerProcessor,
        IBattleLogService battleLog,
        ILogger<HealEffectHandler> logger)
    {
        _statService = statService;
        _triggerProcessor = triggerProcessor;
        _battleLog = battleLog;
        _logger = logger;
    }

    public EffectType SupportedType => EffectType.HEAL;

    public void Apply(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        GameState gameState,
        CombatActionResult actionResult)
    {
        // 1. Calcular a Cura Bruta
        float sourceStatValue = 0;

        // Prioridade ao ScalingStat do JSON, fallback para Magic se não definido
        if (def.ScalingStat != StatType.None)
        {
            sourceStatValue = _statService.GetStatValue(source, def.ScalingStat);
        }
        else
        {
            sourceStatValue = _statService.GetStatValue(source, StatType.Magic);
        }

        float rawHeal = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;
        int healAmount = (int)Math.Round(Math.Max(0, rawHeal)); // Garante que não é negativo

        if (healAmount == 0) return;

        // 2. Aplicar a Cura (Respeitando MaxHP)
        int oldHP = target.CurrentHP;
        target.CurrentHP += healAmount;

        // Clamp ao MaxHP
        if (target.CurrentHP > target.MaxHP)
        {
            target.CurrentHP = target.MaxHP;
        }

        int actualHealed = target.CurrentHP - oldHP;

        // 3. Feedback e Logs
        _logger.LogDebug(
            "Heal Logic: {Source} healed {Target}. Raw: {Raw}, Effective: {Effective}",
            source.Name, target.Name, healAmount, actualHealed);

        if (actualHealed > 0)
        {
            _battleLog.Log($"{source.Name} healed {target.Name} for {actualHealed} HP.");

            //  Tag para a UI 
            actionResult.ResultTags.Add("Heal");

            // 4. Disparar Triggers 
            var context = new TriggerContext
            {
                Source = source,
                Target = target,
                GameState = gameState,
                Value = actualHealed,
                Tags = new HashSet<string>(def.Tags) { "Heal" }
            };

            //Triggers
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEAL_HEAL, context);            
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_RECEIVE_HEAL, context);
        }
        else
        {
            _battleLog.Log($"{source.Name} healed {target.Name} for 0 HP (Already Full).");
        }
    }
}