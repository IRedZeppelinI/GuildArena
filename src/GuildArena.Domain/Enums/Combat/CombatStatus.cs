namespace GuildArena.Domain.Enums.Combat;

/// <summary>
/// Indicates the current state of a combat session.
/// </summary>
public enum CombatStatus
{
    /// <summary>
    /// The combat is still in progress.
    /// </summary>
    Ongoing,

    /// <summary>
    /// Combat ended with Player 1 as the winner.
    /// </summary>
    Player1Won,

    /// <summary>
    /// Combat ended with Player 2 as the winner.
    /// </summary>
    Player2Won,

    /// <summary>
    /// The combat resulted in a draw.
    /// </summary>
    Draw,

    /// <summary>
    /// The combat was prematurely ended by surrender.
    /// </summary>
    Surrendered
}