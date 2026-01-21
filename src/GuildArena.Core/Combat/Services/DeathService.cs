using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.ValueObjects.State;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

public class DeathService : IDeathService
{
    private readonly ITriggerProcessor _triggerProcessor;
    private readonly IModifierDefinitionRepository _modifierRepo;
    private readonly IBattleLogService _battleLog;
    private readonly ILogger<DeathService> _logger;

    public DeathService(
        ITriggerProcessor triggerProcessor,
        IModifierDefinitionRepository modifierRepo,
        IBattleLogService battleLog,
        ILogger<DeathService> logger)
    {
        _triggerProcessor = triggerProcessor;
        _modifierRepo = modifierRepo;
        _battleLog = battleLog;
        _logger = logger;
    }

    public void ProcessDeathIfApplicable(Combatant victim, Combatant killer, GameState gameState)
    {
        if (victim.CurrentHP > 0) return;

        // --- FASE 1: Confirmação e Estado ---
        victim.CurrentHP = 0;

        _logger.LogInformation("Unit {UnitId} ({Name}) died. Killer: {Killer}",
            victim.Id, victim.Name, killer.Name);
        _battleLog.Log($"{victim.Name} has been slain!");

        // --- FASE 2: Death Rattle (Triggers) ---
        // Dispara ANTES da limpeza para que traits explosivos ou "OnDeath" ainda funcionem.
        var deathContext = new TriggerContext
        {
            Source = killer,
            Target = victim,
            GameState = gameState,
            Tags = new HashSet<string> { "Death" }
        };
        _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_DEATH, deathContext);

        // --- FASE 3: Limpeza Externa (Link Severance) ---
        // Remove modifiers noutros combatentes que dependiam deste morto.
        CleanupExternalLinks(victim, gameState);

        // --- FASE 4: Limpeza Interna (Corpse Cleanup) ---
        // Remove buffs temporários do morto, mantendo Traits permanentes (Ressurreição friendly).
        CleanupInternalState(victim);
    }

    private void CleanupExternalLinks(Combatant deadUnit, GameState gameState)
    {
        var definitions = _modifierRepo.GetAllDefinitions();

        foreach (var combatant in gameState.Combatants)
        {
            // Não verificamos o próprio aqui (tratado na Fase 4)
            if (combatant.Id == deadUnit.Id) continue;

            var toRemove = new List<ActiveModifier>();

            foreach (var mod in combatant.ActiveModifiers)
            {
                // Se o modifier foi criado pelo morto
                if (mod.CasterId == deadUnit.Id)
                {
                    if (definitions.TryGetValue(mod.DefinitionId, out var def) && def.RemoveOnCasterDeath)
                    {
                        toRemove.Add(mod);
                    }
                }
            }

            foreach (var mod in toRemove)
            {
                combatant.ActiveModifiers.Remove(mod);
                _logger.LogDebug
                    ("Cleanup: Removed modifier {Mod} from {Target} because caster {Caster} died.",
                    mod.DefinitionId, combatant.Name, deadUnit.Name);
            }
        }
    }

    private void CleanupInternalState(Combatant deadUnit)
    {
        var toRemove = new List<ActiveModifier>();

        foreach (var mod in deadUnit.ActiveModifiers)
        {
            // Remove apenas modifiers temporários (Buffs, Debuffs, CC).
            // Mantém Traits Raciais ou Passivas (TurnsRemaining == -1).
            if (mod.TurnsRemaining != -1)
            {
                toRemove.Add(mod);
            }
        }

        foreach (var mod in toRemove)
        {
            deadUnit.ActiveModifiers.Remove(mod);
        }

        _logger.LogDebug("Cleanup: Removed {Count} temporary modifiers from corpse {Name}.",
            toRemove.Count, deadUnit.Name);
    }
}