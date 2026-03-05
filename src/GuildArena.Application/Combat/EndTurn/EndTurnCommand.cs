using MediatR;

namespace GuildArena.Application.Combat.EndTurn;

/// <summary>
/// Represents a human player's intent to end their current turn in an active combat session.
/// Triggers end-of-turn effects, turn advancement, and potentially AI execution.
/// </summary>
public class EndTurnCommand : IRequest
{
    /// <summary>
    /// The unique identifier (GUID) of the combat session where the turn is being ended.
    /// </summary>
    public required string CombatId { get; set; }
}