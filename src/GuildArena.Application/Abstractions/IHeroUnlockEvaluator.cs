using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects.UnlockHero;
using GuildArena.Shared.DTOs.Shop;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Evaluates unlock conditions against the current state of a guild.
/// </summary>
public interface IHeroUnlockEvaluator
{
    /// <summary>
    /// Checks whether all unlock conditions are satisfied for the given guild.
    /// </summary>
    /// <param name="guild">The guild attempting to unlock.</param>
    /// <param name="requirements">The unlock requirements defined in the hero JSON.</param>
    /// <returns>True if all conditions are met, false otherwise.</returns>
    bool AreConditionsMet(Guild guild, HeroUnlockRequirements requirements);

    /// <summary>
    /// Returns a list of condition progress DTOs for UI display.
    /// </summary>
    /// <param name="guild">The guild to evaluate progress for.</param>
    /// <param name="conditions">The list of conditions from the hero JSON.</param>
    /// <returns>A list of <see cref="UnlockConditionDto"/> with progress values.</returns>
    List<UnlockConditionDto> GetProgress(Guild guild, List<UnlockHeroCondition> conditions);
}