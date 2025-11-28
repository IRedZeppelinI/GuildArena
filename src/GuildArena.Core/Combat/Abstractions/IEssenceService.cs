using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a specialist service for managing player essence, including generation and costs.
/// </summary>
public interface IEssenceService
{
    /// <summary>
    /// Generates the start-of-turn essence (Base + Modifiers) and applies it to the player's pool.
    /// </summary>
    /// <param name="player">The player to receive the essence.</param>
    /// <param name="turnNumber">The current turn number (affects base generation).</param>
    void GenerateStartOfTurnEssence(CombatPlayer player, int turnNumber);


    /// <summary>
    /// Checks if the player has enough essence in their pool to cover the specified costs.
    /// This validates specific colors first, then checks if the remaining total covers neutral costs.
    /// </summary>
    /// <param name="player">The player attempting to pay.</param>
    /// <param name="costs">The calculated invoice (from CostCalculationService).</param>
    /// <returns>True if the player can afford the cost.</returns>
    bool HasEnoughEssence(CombatPlayer player, List<EssenceAmount> costs);


    /// <summary>
    /// Deducts the essence from the player's pool based on a specific payment instruction.
    /// </summary>
    /// <param name="player">The player paying.</param>
    /// <param name="payment">A dictionary mapping EssenceType to the Amount to consume.</param>
    void ConsumeEssence(CombatPlayer player, Dictionary<EssenceType, int> payment);


    /// <summary>
    /// Adds (or removes if negative) a specific amount of essence to the player's pool, respecting the cap.
    /// Used for abilities like Channeling or essence generation.
    /// </summary>
    /// <param name="player">The player to affect.</param>
    /// <param name="type">The type of essence.</param>
    /// <param name="amount">The amount to add.</param>
    void AddEssence(CombatPlayer player, EssenceType type, int amount);
}