using GuildArena.Domain.Enums.Resources;
using GuildArena.Shared.DTOs.Combat;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;

namespace GuildArena.Web.State;

/// <summary>
/// Manages the local state, HTTP communication, and real-time SignalR connection 
/// for an active combat session in the Blazor client.
/// </summary>
public interface ICombatStateService
{
    /// <summary>
    /// Triggered whenever the combat state or battle logs are updated, 
    /// allowing the UI to re-render.
    /// </summary>
    event Action? OnChange;

    string? CombatId { get; }
    MatchType? MatchType { get; }
    GameStateDto? GameState { get; }
    IReadOnlyList<string> BattleLogs { get; }
    bool IsConnecting { get; }
    CombatResultDto? CombatResult { get; }
    bool IsConnected { get; }
    bool IsCommandPending { get; }
    bool RequiresSynchronization { get; }
    bool IsCombatUnavailable { get; }
    string? PendingCommandName { get; }
    CombatCommandResult? LastCommandResult { get; }

    /// <summary>
    /// Initializes a PvE combat session by calling the API and establishing a SignalR connection.
    /// </summary>
    Task<EncounterStartResult> StartEncounterCombatAsync(string encounterId, List<int> heroInstanceIds);

    Task<EncounterStartResult> EnterDungeonCombatAsync();

    /// <summary>
    /// Signals the API that the local player has ended their turn.
    /// </summary>
    Task<CombatCommandResult> EndTurnAsync();

    /// <summary>
    /// Sends a request to the API to execute a specific combat ability.
    /// </summary>
    Task<CombatCommandResult> ExecuteAbilityAsync(
        int sourceId,
        string abilityId,
        Dictionary<string, List<int>> targetSelections,
        Dictionary<EssenceType, int> payment);

    /// <summary>
    /// Sends a request to the API to exchange two existing essences for a new one.
    /// </summary>
    Task<CombatCommandResult> ExchangeEssenceAsync(
        Dictionary<EssenceType, int> spent,
        EssenceType gained);

    /// <summary>
    /// Gracefully disconnects from the SignalR hub and resets the local state.
    /// </summary>
    Task DisconnectAsync();

    Task<CombatCommandResult> SurrenderAsync();

    /// <summary>
    /// Reads current state or confirms that no active combat remains, restoring the subscription
    /// when needed. Never repeats a command; returns false when recovery is still unresolved.
    /// </summary>
    Task<bool> RefreshCombatAsync();


    /// <summary>
    /// Checks if the user is currently in a combat. 
    /// Returns the ActiveCombatDto containing the CombatId and MatchType if true.
    /// </summary>
    Task<ActiveCombatDto?> CheckActiveCombatAsync();

    /// <summary>
    /// Re-fetches the state from the API and connects to the SignalR Hub.
    /// </summary>
    Task<bool> RejoinCombatAsync(string combatId, MatchType? matchType);
}
