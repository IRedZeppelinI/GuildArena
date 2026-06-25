using GuildArena.Domain.Enums.Resources;
using GuildArena.Shared.DTOs.Combat;

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
    GameStateDto? GameState { get; }
    IReadOnlyList<string> BattleLogs { get; }
    bool IsConnecting { get; }
    CombatResultDto? CombatResult { get; }

    /// <summary>
    /// Initializes a PvE combat session by calling the API and establishing a SignalR connection.
    /// </summary>
    Task StartEncounterCombatAsync(string encounterId, List<int> heroInstanceIds);

    Task EnterDungeonCombatAsync();

    /// <summary>
    /// Signals the API that the local player has ended their turn.
    /// </summary>
    Task EndTurnAsync();

    /// <summary>
    /// Sends a request to the API to execute a specific combat ability.
    /// </summary>
    Task ExecuteAbilityAsync(
        int sourceId,
        string abilityId,
        Dictionary<string, List<int>> targetSelections,
        Dictionary<EssenceType, int> payment);

    /// <summary>
    /// Sends a request to the API to exchange two existing essences for a new one.
    /// </summary>
    Task ExchangeEssenceAsync(
        Dictionary<EssenceType, int> spent,
        EssenceType gained);

    /// <summary>
    /// Gracefully disconnects from the SignalR hub and resets the local state.
    /// </summary>
    Task DisconnectAsync();

    Task SurrenderAsync();


    /// <summary>
    /// Checks if the user is currently in a combat. 
    /// Returns the ActiveCombatDto containing the CombatId and MatchType if true.
    /// </summary>
    Task<ActiveCombatDto?> CheckActiveCombatAsync();

    /// <summary>
    /// Re-fetches the state from the API and connects to the SignalR Hub.
    /// </summary>
    Task RejoinCombatAsync(string combatId);
}