using GuildArena.Domain.Entities;
using GuildArena.Domain.ValueObjects;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a specialist service for resolving the final list of combatants 
/// affected by a specific targeting rule.
/// </summary>
public interface ITargetResolutionService
{
    /// <summary>
    /// Resolves the targets for a rule based on game state, source, and player selection.
    /// Handles logic for auto-targeting (Lowest HP, Random) and validations (Untargetable).
    /// </summary>
    /// <param name="rule">The rule defining who can be targeted.</param>
    /// <param name="source">The combatant using the ability.</param>
    /// <param name="gameState">The current combat state.</param>
    /// <param name="playerInput">The selections made by the player (if any).</param>
    /// <returns>A list of valid combatants.</returns>
    List<Combatant> ResolveTargets(
        TargetingRule rule,
        Combatant source,
        GameState gameState,
        AbilityTargets playerInput);
}