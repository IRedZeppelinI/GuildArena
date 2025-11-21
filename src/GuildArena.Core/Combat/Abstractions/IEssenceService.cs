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
    /// This validates both specific colors and total amounts (for neutral costs).
    /// </summary>
    bool HasEnoughEssence(CombatPlayer player, List<EssenceCost> costs);


    /// <summary>
    /// Deducts the essence from the player's pool.
    /// IMPORTANT: The 'payment' list must specify exactly which essence types to consume.
    /// Neutral costs in the definition should be resolved to concrete types in the 'payment' list before calling this.
    /// </summary>
    void PayEssence(CombatPlayer player, Dictionary<EssenceType, int> payment);
}