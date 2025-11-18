using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

public class TurnManagerService : ITurnManagerService
{
    private readonly ILogger<TurnManagerService> _logger;

    public TurnManagerService(ILogger<TurnManagerService> logger)
    {
        _logger = logger;
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
            .ToList(); //

        foreach (var combatant in combatantsEndingTurn)
        {
            TickCooldowns(combatant); //
            TickModifiers(combatant); //
            // (Futuro: Triggers ON_TURN_END)
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

        //TODO: Implementar a lógica de Essence no início do turno,
        //        quando o mecanismo de Essence for definido.

        _logger.LogInformation(
            "Turn advanced. It is now Player {PlayerId}'s turn.",
            newPlayer.PlayerId);

        // (Futuro: Triggers ON_TURN_START para os combatentes do novo jogador)
    }

    // --- Métodos Helper Privados (TickCooldowns, TickModifiers) ---
    
    private void TickCooldowns(Combatant combatant)
    {
        // Itera ao contrário para poder remover itens da lista em segurança
        for (int i = combatant.ActiveCooldowns.Count - 1; i >= 0; i--)
        {
            var cd = combatant.ActiveCooldowns[i]; //
            cd.TurnsRemaining--; //

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
            var mod = combatant.ActiveModifiers[i]; //

            // Ignora modifiers permanentes/passivos
            if (mod.TurnsRemaining == -1) continue;

            mod.TurnsRemaining--; //

            if (mod.TurnsRemaining <= 0)
            {
                combatant.ActiveModifiers.RemoveAt(i);
            }
        }
    }
}