using GuildArena.Domain.Gameplay;

namespace GuildArena.Application.Abstractions.Notifications;

/// <summary>
/// Defines the contract for pushing real-time combat updates to clients.
/// The implementation (e.g., SignalR) will reside in the API/Presentation layer.
/// </summary>
public interface ICombatNotifier
{
    /// <summary>
    /// Broadcasts narrative battle logs to the clients involved in the combat.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat session.</param>
    /// <param name="logs">The list of ordered event logs.</param>
    Task SendBattleLogsAsync(string combatId, List<string> logs);

    /// <summary>
    /// Broadcasts the updated game state to the clients.
    /// Implementations should map the Domain GameState to a secure DTO before sending.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat session.</param>
    /// <param name="state">The current domain state of the combat.</param>
    Task SendGameStateUpdateAsync(string combatId, GameState state);
}