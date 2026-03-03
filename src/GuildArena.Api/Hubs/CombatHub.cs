using Microsoft.AspNetCore.SignalR;

namespace GuildArena.Api.Hubs;

/// <summary>
/// Manages real-time WebSocket connections for combat sessions.
/// </summary>
public class CombatHub : Hub
{
    /// <summary>
    /// Called by the client to subscribe to updates for a specific combat.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat session.</param>
    public async Task JoinCombat(string combatId)
    {
        // Adiciona a ligação atual a um "Grupo" com o nome do combatId.
        await Groups.AddToGroupAsync(Context.ConnectionId, combatId);
    }

    /// <summary>
    /// Called by the client to unsubscribe from updates.
    /// </summary>
    /// <param name="combatId">The unique ID of the combat session.</param>
    public async Task LeaveCombat(string combatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, combatId);
    }
}