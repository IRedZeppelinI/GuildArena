using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;
using System;

namespace GuildArena.Core.Combat.Services;

public class TurnManagerService : ITurnManagerService
{
    private readonly ILogger<TurnManagerService> _logger;
    private readonly IEssenceService _essenceService;

    public TurnManagerService(ILogger<TurnManagerService> logger, IEssenceService essenceService)
    {
        _logger = logger;
        _essenceService = essenceService;
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
            TickCooldowns(combatant); 
            TickModifiers(combatant); 
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

        _essenceService.GenerateStartOfTurnEssence(newPlayer, gameState.CurrentTurnNumber);

        _logger.LogInformation(
            "Turn advanced to Player {PlayerId}.",
            newPlayer.PlayerId);

        // Log do estado atual da Essence (para debug)
        foreach (var kvp in newPlayer.EssencePool)
        {
            if (kvp.Value > 0)
                _logger.LogDebug("Essence {Type}: {Amount}", kvp.Key, kvp.Value);
        }

        // TODO: Triggers ON_TURN_START para os combatentes do novo jogador)
    }

    // --- Helper Essence ---
    private void GenerateTurnEssence(CombatPlayer player, int turnNumber)
    {
        // Regra: 2 no primeiro turno, 4 nos seguintes.
        // Nota: Assumimos que 'TurnNumber 1' é o primeiro turno de TODOS.
        int amountToGenerate = (turnNumber == 1) ? 2 : 4;

        for (int i = 0; i < amountToGenerate; i++)
        {
            // Verificar Cap Máximo (se aplicável)
            int currentTotal = player.EssencePool.Values.Sum();
            if (currentTotal >= player.MaxTotalEssence)
            {
                _logger.LogInformation("Essence cap reached for Player {Id}", player.PlayerId);
                break;
            }

            // Gerar tipo aleatório
            EssenceType randomType = GetRandomEssenceType();

            // Adicionar ao dicionário
            if (!player.EssencePool.ContainsKey(randomType))
            {
                player.EssencePool[randomType] = 0;
            }
            player.EssencePool[randomType]++;
        }
    }

    // --- Helper Essence ---

    private EssenceType GetRandomEssenceType()
    {
        // Array com todos os tipos MENOS Neutral (Neutral é custo, não recurso gerado)
        var validTypes = new[]
        {
            EssenceType.Vigor,
            EssenceType.Mind,
            EssenceType.Light,
            EssenceType.Shadow,
            EssenceType.Flux
        };

        int index = _random.Next(validTypes.Length);
        return validTypes[index];
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