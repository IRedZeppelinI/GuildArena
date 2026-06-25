using MatchType = GuildArena.Domain.Enums.Matches.MatchType;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Data Transfer Object representing the presence of an ongoing combat session for a player.
/// Used by the client to determine if it needs to trigger the reconnection flow.
/// </summary>
public class ActiveCombatDto
{
    /// <summary>
    /// The unique identifier (GUID) of the active combat in Redis, if any.
    /// </summary>
    public string? CombatId { get; set; }

    /// <summary>
    /// Convenience property that returns true if the player is currently in an active combat.
    /// </summary>
    public bool HasActiveCombat => !string.IsNullOrEmpty(CombatId);

    /// <summary>
    /// The type of match (e.g., Encounter, Dungeon, PvP) the player is currently in.
    /// Allows the frontend to route the player to the correct Arena UI or apply specific rules.
    /// Null if the player is not in a combat.
    /// </summary>
    public MatchType? MatchType { get; set; }
}