using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.Resources;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Implements the logic for essence generation and management based on game rules and modifiers.
/// </summary>
public class EssenceService : IEssenceService
{
    private readonly IModifierDefinitionRepository _modifierRepository;
    private readonly ILogger<EssenceService> _logger;
    private readonly IRandomProvider _random;
    private readonly IBattleLogService _battleLog;

    public EssenceService(
        IModifierDefinitionRepository modifierRepository,
        ILogger<EssenceService> logger,
        IRandomProvider random,
        IBattleLogService battleLog)
    {
        _modifierRepository = modifierRepository;
        _logger = logger;
        _random = random;
        _battleLog = battleLog;
    }




    /// <inheritdoc />
    public void AddEssence(CombatPlayer player, EssenceType type, int amount)
    {
        // 1. RESOLVER RANDOM 
        if (type == EssenceType.Random)
        {
            if (amount > 0)
            {
                // Ganhar: Escolhe uma cor qualquer
                type = GetRandomEssenceType();
            }
            else if (amount < 0)
            {
                // Perder: Só pode perder o que TEM
                if (TryGetExistingRandomEssence(player, out var existingType))
                {
                    type = existingType;
                }
                else
                {
                    _logger.LogInformation("Player {Id} has no essence to remove (Random selection).", player.PlayerId);
                    return; // Não tem nada para tirar
                }
            }
            // Se amount for 0, ignora
        }

        // 2. ADICIONAR / REMOVER
        // Se for adicionar (Buff), verifica Cap
        if (amount > 0)
        {
            int currentTotal = player.EssencePool.Values.Sum();
            if (currentTotal >= player.MaxTotalEssence)
            {
                // Cap atingido
                return;
            }

            int spaceLeft = player.MaxTotalEssence - currentTotal;
            if (amount > spaceLeft) amount = spaceLeft;
        }

        if (!player.EssencePool.ContainsKey(type))
        {
            player.EssencePool[type] = 0;
        }

        player.EssencePool[type] += amount;

        // Clamp a zero (para não ter essence negativa)
        if (player.EssencePool[type] < 0)
            player.EssencePool[type] = 0;

        if (amount > 0)
        {
            _battleLog.Log($"{player.Name} gained {amount} {type} Essence.");
        }
        else if (amount < 0)
        {
            _battleLog.Log($"{player.Name} lost {Math.Abs(amount)} {type} Essence.");
        }

        if (amount != 0)
        {
            _logger.LogDebug(
                "Player {Id} with {Name} essence change: {Amount} {Type}. New Total: {Total}",
                player.PlayerId, player.Name, amount, type, player.EssencePool[type]);
        }
    }



    /// <summary>
    /// Generates the start-of-turn essence (Base + Modifiers) and applies it to the player's pool.
    /// </summary>

    public void GenerateStartOfTurnEssence(CombatPlayer player, int baseAmount = 4)
    {
        // 1. GERAÇÃO BASE       

        for (int i = 0; i < baseAmount; i++)
        {            
            AddEssence(player, EssenceType.Random, 1);
        }

        // 2. GERAÇÃO VIA MODIFIERS
        var allDefinitions = _modifierRepository.GetAllDefinitions();
        foreach (var activeMod in player.ActiveModifiers)
        {
            if (!allDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef)) continue;

            foreach (var genRule in modDef.EssenceGenerationModifications)
            {                
                var typeToGen = genRule.IsRandom ? EssenceType.Random : genRule.EssenceType;

                AddEssence(player, typeToGen, genRule.Amount);                
            }
        }
    }

    public bool HasEnoughEssence(CombatPlayer player, List<EssenceAmount> costs)
    {
        var tempPool = new Dictionary<EssenceType, int>(player.EssencePool);

        foreach (var cost in costs.Where(c => c.Type != EssenceType.Neutral))
        {
            if (!tempPool.TryGetValue(cost.Type, out int available) || available < cost.Amount)
                return false;
            tempPool[cost.Type] -= cost.Amount;
        }

        int neutralNeeded = costs.Where(c => c.Type == EssenceType.Neutral).Sum(c => c.Amount);
        if (neutralNeeded > 0)
        {
            int totalAvailable = tempPool.Values.Sum();
            if (totalAvailable < neutralNeeded) return false;
        }

        return true;
    }

    /// <summary>
    /// Deducts the essence from the player's pool based on a specific payment instruction.
    /// </summary>
    public void ConsumeEssence(CombatPlayer player, Dictionary<EssenceType, int> payment)
    {
        foreach (var kvp in payment)
        {
            if (player.EssencePool.TryGetValue(kvp.Key, out int current))
            {
                player.EssencePool[kvp.Key] = Math.Max(0, current - kvp.Value);
            }
        }
    }






    



    //  Helpers     
    private bool TryGetExistingRandomEssence(CombatPlayer player, out EssenceType type)
    {
        var availableTypes = player.EssencePool
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => kvp.Key)
            .ToList();

        if (availableTypes.Count == 0)
        {
            type = default;
            return false;
        }
        type = availableTypes[_random.Next(availableTypes.Count)];
        return true;
    }

    private EssenceType GetRandomEssenceType()
    {
        var validTypes = new[]
        {
            EssenceType.Vigor, EssenceType.Mind, EssenceType.Light,
            EssenceType.Shadow, EssenceType.Flux
        };
        return validTypes[_random.Next(validTypes.Length)];
    }


}