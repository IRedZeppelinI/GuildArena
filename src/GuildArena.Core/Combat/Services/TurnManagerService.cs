using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;
using System;

namespace GuildArena.Core.Combat.Services;

public class TurnManagerService : ITurnManagerService
{
    private readonly ILogger<TurnManagerService> _logger;
    private readonly IEssenceService _essenceService;
    private readonly ITriggerProcessor _triggerProcessor;

    public TurnManagerService(
        ILogger<TurnManagerService> logger,
        IEssenceService essenceService,
        ITriggerProcessor triggerProcessor)
    {
        _logger = logger;
        _essenceService = essenceService;
        _triggerProcessor = triggerProcessor;
    }

    /// <summary>
    /// Advances the combat state to the next player's turn,
    /// applying all end-of-turn and start-of-turn effects.
    /// </summary>
    public void AdvanceTurn(GameState gameState)
    {
        var playerWhoseTurnEnded = gameState.CurrentPlayerId;
        _logger.LogInformation(
            "Advancing turn. Player {PlayerId} is ending their turn. Turn number {Turn}",
            playerWhoseTurnEnded, gameState.CurrentTurnNumber);

        // 1. Processar o FIM do turno para o jogador atual
        var combatantsEndingTurn = gameState.Combatants
            .Where(c => c.OwnerId == playerWhoseTurnEnded && c.IsAlive)
            .ToList(); 

        foreach (var combatant in combatantsEndingTurn)
        {
            // Disparar Trigger ON_TURN_END
            // O Source e Target são o próprio combatente neste contexto
            var context = new TriggerContext
            {
                Source = combatant,
                Target = combatant,
                GameState = gameState,
                Value = null,
                Tags = new HashSet<string> { "TurnEnd" }
            };
            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_TURN_END, context);

            TickCooldowns(combatant); 
            TickModifiers(combatant);             
        }

        // 2. Encontrar o PRÓXIMO jogador (Lógica Round-Robin)
        var playerIds = gameState.Players.Select(p => p.PlayerId).ToList();

        int currentIndex = playerIds.IndexOf(playerWhoseTurnEnded);
        int nextIndex = (currentIndex + 1) % playerIds.Count;
        var nextPlayerId = playerIds[nextIndex];

        // 3. Mudar o estado para o PRÓXIMO jogador
        gameState.CurrentPlayerId = nextPlayerId;

        // 4. Processar o INÍCIO do turno para o novo jogador

        if (nextIndex == 0)
        {
            gameState.CurrentTurnNumber++;
            _logger.LogInformation("--- New Round Started: {Turn} ---", gameState.CurrentTurnNumber);
        }

        var newPlayer = gameState.Players.First(p => p.PlayerId == nextPlayerId);

        //gerar essence de novo turno
        _essenceService.GenerateStartOfTurnEssence(newPlayer);

        // Processar Start Turn Triggers        
        var combatantsStartingTurn = gameState.Combatants
            .Where(c => c.OwnerId == nextPlayerId && c.IsAlive)
            .ToList();

        foreach (var combatant in combatantsStartingTurn)
        {
            //reset ás actions
            combatant.ActionsTakenThisTurn = 0;

            var context = new TriggerContext
            {
                Source = combatant,
                Target = combatant,
                GameState = gameState,
                Value = null,
                Tags = new HashSet<string> { "TurnStart" }
            };

            _triggerProcessor.ProcessTriggers(ModifierTrigger.ON_TURN_START, context);
        }

        _logger.LogInformation(
            "Turn advanced to Player {PlayerId}.",
            newPlayer.PlayerId);

        // Log do estado atual da Essence (para debug)
        foreach (var kvp in newPlayer.EssencePool)
        {
            if (kvp.Value > 0)
                _logger.LogDebug("Essence {Type}: {Amount}", kvp.Key, kvp.Value);
        }        
    }   



    // --- Métodos Helper Privados Cooldowns ---

    private void TickCooldowns(Combatant combatant)
    {
        // Itera ao contrário para poder remover itens da lista em segurança
        for (int i = combatant.ActiveCooldowns.Count - 1; i >= 0; i--)
        {
            var cd = combatant.ActiveCooldowns[i]; 
            cd.TurnsRemaining--; 

            if (cd.TurnsRemaining <= 0)
            {
                combatant.ActiveCooldowns.RemoveAt(i);
            }
        }
    }

    private void TickModifiers(Combatant combatant)
    {
        for (int i = combatant.ActiveModifiers.Count - 1; i >= 0; i--)
        {
            var mod = combatant.ActiveModifiers[i]; 

            // Ignora modifiers permanentes/passivos
            if (mod.TurnsRemaining == -1) continue;

            mod.TurnsRemaining--; 

            if (mod.TurnsRemaining <= 0)
            {
                combatant.ActiveModifiers.RemoveAt(i);
            }
        }
    }
}