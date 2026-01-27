using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Resources;

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
    /// <param name="baseAmount">The current turn number (affects base generation).</param>
    void GenerateStartOfTurnEssence(CombatPlayer player, int baseAmount = 4);


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
    /// Directly modifies the essence pool of the specified player.
    /// <para>
    /// If <paramref name="amount"/> is positive, essence is added up to the player's cap.
    /// If <paramref name="amount"/> is negative, essence is removed (clamped to zero).
    /// </para>
    /// <para>
    /// If <paramref name="type"/> is <see cref="EssenceType.Random"/>, the service automatically 
    /// selects a random type to add (generated) or remove (from existing pool).
    /// </para>
    /// </summary>
    /// <param name="player">The player whose pool will be modified.</param>
    /// <param name="type">The specific type of essence, or <see cref="EssenceType.Random"/>.</param>
    /// <param name="amount">The quantity to add (positive) or remove (negative).</param>
    void AddEssence(CombatPlayer player, EssenceType type, int amount);
}