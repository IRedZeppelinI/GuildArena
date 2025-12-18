using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Modifiers;
using Microsoft.Extensions.Logging;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;

namespace GuildArena.Core.Combat.Handlers;

public class ApplyModifierHandler : IEffectHandler
{
    private readonly ILogger<ApplyModifierHandler> _logger;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IStatCalculationService _statService;
    private readonly IBattleLogService _battleLog;

    public ApplyModifierHandler(
        ILogger<ApplyModifierHandler> logger,
        IModifierDefinitionRepository modifierRepo,
        IStatCalculationService statService,
        IBattleLogService battleLog)
    {
        _logger = logger;
        _modifierRepo = modifierRepo;
        _statService = statService;
        _battleLog = battleLog;
    }

    public EffectType SupportedType => EffectType.APPLY_MODIFIER;

    public void Apply(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        GameState gameState,
        CombatActionResult actionResult)
    {
        if (string.IsNullOrEmpty(def.ModifierDefinitionId))
        {
            _logger.LogWarning("ApplyModifierHandler: Missing ModifierDefinitionId.");
            return;
        }

        var definitions = _modifierRepo.GetAllDefinitions();
        if (!definitions.TryGetValue(def.ModifierDefinitionId, out var modDef)) return;

        // Calcular valor da Barreira (se houver)
        float initialBarrierValue = 0;
        if (modDef.Barrier != null)
        {
            initialBarrierValue = CalculateBarrierValue(source, modDef.Barrier);
            initialBarrierValue = ApplyBarrierBonuses(source, initialBarrierValue, definitions);
        }

        var existingModifier = target.ActiveModifiers
            .FirstOrDefault(m => m.DefinitionId == def.ModifierDefinitionId);

        if (existingModifier != null)
        {
            // Lógica de Refresh
            existingModifier.TurnsRemaining = def.DurationInTurns;

            if (initialBarrierValue > 0)
            {
                existingModifier.CurrentBarrierValue = initialBarrierValue;
                // --- BATTLE LOG ---
                _battleLog.Log
                    ($"{target.Name}'s {modDef.Name} was refreshed (Barrier: {initialBarrierValue}).");
            }
            else
            {
                // --- BATTLE LOG ---
                _battleLog.Log($"{target.Name}'s {modDef.Name} was refreshed.");
            }

            existingModifier.ActiveStatusEffects = modDef.GrantedStatusEffects.ToList();
        }
        else
        {
            // Lógica de Nova Aplicação
            var newActiveModifier = new ActiveModifier
            {
                DefinitionId = def.ModifierDefinitionId,
                TurnsRemaining = def.DurationInTurns,
                CasterId = source.Id,
                CurrentBarrierValue = initialBarrierValue,
                ActiveStatusEffects = modDef.GrantedStatusEffects.ToList()
            };

            target.ActiveModifiers.Add(newActiveModifier);

            // --- BATTLE LOG ---
            // Distinção simples de texto baseada no tipo (Buff/Curse)
            if (modDef.Type == ModifierType.Bless) // Assumindo Bless como Buff positivo
                _battleLog.Log($"{target.Name} gained {modDef.Name}.");
            else
                _battleLog.Log($"{target.Name} is afflicted by {modDef.Name}!");
        }
    }

    private float CalculateBarrierValue(Combatant source, BarrierProperties barrierDef)
    {
        float statVal = 0;
        if (barrierDef.ScalingFactor > 0)
        {
            statVal = _statService.GetStatValue(source, barrierDef.ScalingStat);
        }
        return (statVal * barrierDef.ScalingFactor) + barrierDef.BaseAmount;
    }

    private float ApplyBarrierBonuses(
        Combatant source,
        float baseValue,
        IReadOnlyDictionary<string, ModifierDefinition> definitions)
    {
        float flatBonus = 0;
        float percentBonus = 0;

        foreach (var mod in source.ActiveModifiers)
        {
            if (definitions.TryGetValue(mod.DefinitionId, out var def))
            {
                foreach (var barMod in def.BarrierModifications)
                {
                    if (barMod.Type == ModificationType.FLAT) flatBonus += barMod.Value;
                    else percentBonus += barMod.Value;
                }
            }
        }
        return (baseValue + flatBonus) * (1 + percentBonus);
    }
}