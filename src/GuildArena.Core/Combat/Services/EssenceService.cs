using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// Implements the logic for essence generation and management based on game rules and modifiers.
/// </summary>
public class EssenceService : IEssenceService
{
    private readonly IModifierDefinitionRepository _modifierRepository;
    private readonly ILogger<EssenceService> _logger;
    private readonly Random _random = new();

    public EssenceService(
        IModifierDefinitionRepository modifierRepository,
        ILogger<EssenceService> logger)
    {
        _modifierRepository = modifierRepository;
        _logger = logger;
    }

    /// <summary>
    /// Generates the start-of-turn essence (Base + Modifiers) and applies it to the player's pool.
    /// </summary>
    public void GenerateStartOfTurnEssence(CombatPlayer player, int turnNumber)
    {
        var allDefinitions = _modifierRepository.GetAllDefinitions();

        // 1. GERAÇÃO BASE (Aleatória) - Primerio turno 2 essence, restantes turnos 4
        int baseAmount = (turnNumber == 1) ? 2 : 4;

        for (int i = 0; i < baseAmount; i++)
        {
            ApplyEssenceChange(player, GetRandomEssenceType(), 1);
        }

        // 2. GERAÇÃO VIA MODIFIERS (Fixa)
        foreach (var activeMod in player.ActiveModifiers)
        {
            if (!allDefinitions.TryGetValue(activeMod.DefinitionId, out var modDef))
                continue;

            foreach (var genRule in modDef.EssenceGenerationModifications)
            {
                // CASO A: Aleatório (Random)
                if (genRule.IsRandom)
                {
                    if (genRule.Amount > 0)
                    {
                        // Buff Aleatório: Ganha qualquer tipo
                        EssenceType type = GetRandomEssenceType();
                        ApplyEssenceChange(player, type, genRule.Amount);
                        LogChange(player, type, genRule.Amount, activeMod.DefinitionId, true);
                    }
                    else if (genRule.Amount < 0)
                    {
                        // Debuff Aleatório: Remove algo QUE EXISTA
                        // Se não tiver essence nenhuma, não acontece nada.
                        if (TryGetExistingRandomEssence(player, out EssenceType typeToRemove))
                        {
                            ApplyEssenceChange(player, typeToRemove, genRule.Amount);
                            LogChange(player, typeToRemove, genRule.Amount, activeMod.DefinitionId, true);
                        }
                    }
                }
                // CASO B: Fixo (Specific Type)
                else
                {
                    // Buff ou Debuff específico (ex: +1 Vigor ou -1 Vigor)
                    ApplyEssenceChange(player, genRule.EssenceType, genRule.Amount);
                    LogChange(player, genRule.EssenceType, genRule.Amount, activeMod.DefinitionId, false);
                }
            }
        }
    }

    // --- Helpers Privados (Copiados do TurnManager) ---

    private void ApplyEssenceChange(CombatPlayer player, EssenceType type, int amount)
    {
        // Se for adicionar (Buff), verifica Cap
        if (amount > 0)
        {
            int currentTotal = player.EssencePool.Values.Sum();
            if (currentTotal >= player.MaxTotalEssence) return;
        }

        if (!player.EssencePool.ContainsKey(type))
        {
            player.EssencePool[type] = 0;
        }

        player.EssencePool[type] += amount;

        // Clamp a zero (para Debuffs não criarem essence negativa)
        if (player.EssencePool[type] < 0)
            player.EssencePool[type] = 0;
    }

    // --- Helper para encontrar essence existente (para debuffs random) ---
    private bool TryGetExistingRandomEssence(CombatPlayer player, out EssenceType type)
    {
        // Lista apenas os tipos que o jogador REALMENTE tem (> 0)
        var availableTypes = player.EssencePool
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => kvp.Key)
            .ToList();

        if (availableTypes.Count == 0)
        {
            type = default;
            return false; // Jogador já sem essence
        }

        int index = _random.Next(availableTypes.Count);
        type = availableTypes[index];
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

    private void LogChange(CombatPlayer p, EssenceType t, int amount, string modId, bool isRandom)
    {
        _logger.LogDebug("Player {Id} {Action} {Amount} {Type} (Rule: {Rule}) from {Mod}",
            p.PlayerId,
            amount > 0 ? "gained" : "lost",
            Math.Abs(amount),
            t,
            isRandom ? "Random" : "Fixed",
            modId);
    }
}