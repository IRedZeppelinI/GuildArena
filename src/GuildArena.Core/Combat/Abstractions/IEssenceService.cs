using GuildArena.Domain.Entities;

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

    // TODO: bool CanPay(CombatPlayer player, List<EssenceCost> costs);)
    // TODO: void Pay(CombatPlayer player, List<EssenceCost> costs);)
}