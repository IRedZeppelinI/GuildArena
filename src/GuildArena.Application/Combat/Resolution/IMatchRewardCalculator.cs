using GuildArena.Domain.Entities;
using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.Combat.Resolution;

/// <summary>
/// Strategy contract for calculating and applying post-combat rewards
/// based on the match type.
/// </summary>
public interface IMatchRewardCalculator
{
    /// <summary>
    /// Indicates whether this calculator can process the given match type.
    /// </summary>
    bool CanHandle(GuildArena.Domain.Enums.Matches.MatchType matchType);

    /// <summary>
    /// Calculates and applies rewards (gold & XP) to the player's guild,
    /// taking into account victory or defeat.
    /// Returns the reward summary.
    /// </summary>
    /// <param name="gameState">The completed combat state.</param>
    /// <param name="playerGuild">The player's guild entity to be modified.</param>
    /// <param name="isWinner"><c>true</c> if the player won the match.</param>
    MatchRewardResult CalculateAndApplyRewards(GameState gameState, Guild playerGuild, bool isWinner);
}