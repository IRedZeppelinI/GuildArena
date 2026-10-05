using GuildArena.Domain.Enums.Resources;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.Requests;
using GuildArena.Shared.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;

namespace GuildArena.Web.State;

public class CombatStateService : ICombatStateService, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<CombatStateService> _logger;
    private HubConnection? _hubConnection;
    private readonly List<string> _battleLogs = new();

    public event Action? OnChange;

    public string? CombatId { get; private set; }
    public MatchType? MatchType { get; private set; }
    public GameStateDto? GameState { get; private set; }
    public IReadOnlyList<string> BattleLogs => _battleLogs.AsReadOnly();
    public bool IsConnecting { get; private set; }
    public CombatResultDto? CombatResult { get; private set; }
    public bool IsConnected => _isSubscribed && _hubConnection?.State == HubConnectionState.Connected;
    public bool IsCommandPending { get; private set; }
    public bool RequiresSynchronization { get; private set; }
    public bool IsCombatUnavailable { get; private set; }
    public string? PendingCommandName { get; private set; }
    public CombatCommandResult? LastCommandResult { get; private set; }
    private long _stateNotifications;
    private long _sessionGeneration;
    private long _subscriptionGeneration;
    private CancellationTokenSource _sessionCancellation = new();
    private Task<bool>? _synchronizationTask;
    private bool _automaticSynchronizationBlocked;
    private bool _isSubscribed;
    private Task<bool>? _rejoinTask;
    private string? _rejoiningCombatId;

    public CombatStateService(HttpClient http, ILogger<CombatStateService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<EncounterStartResult> StartEncounterCombatAsync(string encounterId, List<int> heroInstanceIds)
    {
        if (IsCommandPending || IsConnecting) return EncounterStartResult.Unconfirmed;
        var session = BeginSession();
        IsConnecting = true;
        NotifyStateChanged();

        try
        {
            var request = new StartEncounterRequest
            {
                EncounterId = encounterId,
                HeroInstanceIds = heroInstanceIds
            };

            using var response = await _http.PostAsJsonAsync("api/combat/start-encounter", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartCombatResponse>();
                if (!IsCurrentSession(session)) return EncounterStartResult.Unconfirmed;
                if (result is { InitialState: not null } && !string.IsNullOrWhiteSpace(result.CombatId))
                {
                    CombatId = result.CombatId;
                    IsCombatUnavailable = false;
                    MatchType = GuildArena.Domain.Enums.Matches.MatchType.Encounter;
                    _battleLogs.Clear();
                    _battleLogs.AddRange(result.InitialLogs);
                    GameState = result.InitialState;

                    await ConnectToSignalRAsync(CombatId, session);
                    if (!await SynchronizeAsync() || !IsCurrentSession(session)) return EncounterStartResult.Unconfirmed;
                    return EncounterStartResult.Ready;
                }
                return EncounterStartResult.Unconfirmed;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to start combat. API returned {StatusCode}: {Error}", response.StatusCode, error);
                return EncounterStartResult.Rejected;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while starting the combat session.");
            return EncounterStartResult.Unconfirmed;
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                IsConnecting = false;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
    }

    public async Task<EncounterStartResult> EnterDungeonCombatAsync()
    {
        if (IsCommandPending || IsConnecting) return EncounterStartResult.Unconfirmed;
        var session = BeginSession();
        IsConnecting = true;
        NotifyStateChanged();

        try
        {
            // Chama o endpoint que inicializa a partida e o estado no Redis
            using var response = await _http.PostAsync("api/dungeon/enter-stage", null);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartCombatResponse>();
                if (!IsCurrentSession(session)) return EncounterStartResult.Unconfirmed;
                if (result is { InitialState: not null } && !string.IsNullOrWhiteSpace(result.CombatId))
                {
                    CombatId = result.CombatId;
                    IsCombatUnavailable = false;
                    MatchType = GuildArena.Domain.Enums.Matches.MatchType.Dungeon;
                    _battleLogs.Clear();
                    _battleLogs.AddRange(result.InitialLogs);
                    GameState = result.InitialState;

                    // Conecta ao WebSockets do combate
                    await ConnectToSignalRAsync(CombatId, session);
                    if (!await SynchronizeAsync() || !IsCurrentSession(session)) return EncounterStartResult.Unconfirmed;
                    return EncounterStartResult.Ready;
                }
                return EncounterStartResult.Unconfirmed;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to enter dungeon stage. API returned {StatusCode}: {Error}", response.StatusCode, error);
                return EncounterStartResult.Rejected;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while entering dungeon stage.");
            return EncounterStartResult.Unconfirmed;
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                IsConnecting = false;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
    }


    public Task<CombatCommandResult> EndTurnAsync() => SendCommandAsync("Ending turn", "end-turn", null);

    public Task<CombatCommandResult> ExecuteAbilityAsync(
        int sourceId,
        string abilityId,
        Dictionary<string, List<int>> targetSelections,
        Dictionary<EssenceType, int> payment)
    {
        var request = new ExecuteAbilityRequest
        {
            CombatId = CombatId ?? "",
            SourceId = sourceId,
            AbilityId = abilityId,
            TargetSelections = targetSelections,
            Payment = payment
        };

        return SendCommandAsync("Using ability", "execute-ability", request);
    }

    public Task<CombatCommandResult> ExchangeEssenceAsync(Dictionary<EssenceType, int> spent, EssenceType gained)
    {
        var request = new ExchangeEssenceRequest
        {
            CombatId = CombatId ?? "",
            EssenceToSpend = spent,
            EssenceToGain = gained
        };

        return SendCommandAsync("Exchanging essences", "exchange-essence", request);
    }

    private async Task<CombatCommandResult> SendCommandAsync(string name, string endpoint, object? request)
    {
        if (IsCommandPending || IsConnecting || RequiresSynchronization || !IsConnected ||
            string.IsNullOrEmpty(CombatId) || IsCombatUnavailable || CombatResult is not null)
            return new(CombatCommandOutcome.Unavailable, "Actions are unavailable until combat is synchronized.");

        IsCommandPending = true;
        var session = _sessionGeneration;
        var combatId = CombatId;
        CombatCommandResult result;
        PendingCommandName = name;
        LastCommandResult = null;
        NotifyStateChanged();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = request is null
                ? await _http.PostAsync($"api/combat/{combatId}/{endpoint}", null, timeout.Token)
                : await _http.PostAsJsonAsync($"api/combat/{combatId}/{endpoint}", request, timeout.Token);

            if (response.IsSuccessStatusCode)
                result = new(CombatCommandOutcome.Accepted, "Action confirmed by the server.");
            else if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                result = new(CombatCommandOutcome.Unconfirmed, "The server did not confirm the outcome. Refresh combat before choosing another action.");
            else
                result = new(CombatCommandOutcome.Rejected, await ReadRejectionAsync(response, timeout.Token));

            if (!IsCurrentSession(session)) return result;
            LastCommandResult = result;

            // Neither a finished await nor an HTTP acknowledgement supplies fresh eligibility.
            RequiresSynchronization = true;
            if (result.Outcome != CombatCommandOutcome.Unconfirmed && CombatResult is null)
                await SynchronizeAsync();
            else if (result.Outcome == CombatCommandOutcome.Unconfirmed)
                _automaticSynchronizationBlocked = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Combat command {Command} could not be confirmed.", endpoint);
            result = new(CombatCommandOutcome.Unconfirmed, "Connection lost or request timed out. The action may have completed. Refresh combat; it will not be sent again.");
            if (IsCurrentSession(session))
            {
                RequiresSynchronization = true;
                _automaticSynchronizationBlocked = true;
                LastCommandResult = result;
            }
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                IsCommandPending = false;
                PendingCommandName = null;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
        return result;
    }

    private static async Task<string> ReadRejectionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.ValueKind == JsonValueKind.Object && problem.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString()!;
        }
        catch (JsonException) { }
        return $"The server rejected the action (HTTP {(int)response.StatusCode}). Review the current state and battle log before trying again.";
    }

    private Task<bool> SynchronizeAsync()
    {
        if (_synchronizationTask is { IsCompleted: false }) return _synchronizationTask;
        if (string.IsNullOrEmpty(CombatId) || !IsConnected) return Task.FromResult(false);
        return _synchronizationTask = SynchronizeCoreAsync(_sessionGeneration, _subscriptionGeneration, CombatId, _sessionCancellation.Token);
    }

    private void TrySynchronizeAutomatically()
    {
        if (RequiresSynchronization && !IsCommandPending && !IsConnecting && IsConnected &&
            !IsCombatUnavailable && CombatResult is null && !_automaticSynchronizationBlocked &&
            LastCommandResult?.Outcome != CombatCommandOutcome.Unconfirmed)
            _ = SynchronizeAsync();
    }

    private async Task<bool> SynchronizeCoreAsync(long session, long subscription, string combatId, CancellationToken sessionCancellation)
    {
        // Publish the shared task before any notification can request another read.
        await Task.Yield();
        if (!IsCurrentSession(session)) return false;
        RequiresSynchronization = true;
        NotifyStateChanged();
        var synchronized = false;
        try
        {
            if (!IsCurrentSubscription(subscription) || !IsConnected) return false;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var notifications = _stateNotifications;
                using var response = await _http.GetAsync($"api/combat/{combatId}", timeout.Token);
                if (!IsCurrentSession(session)) return false;
                if (CombatResult is not null) return synchronized = true;
                if (!IsCurrentSubscription(subscription) || !IsConnected) return false;
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return synchronized = await ConfirmNoActiveCombatAsync(session, subscription);
                if (!response.IsSuccessStatusCode) return false;
                var result = await response.Content.ReadFromJsonAsync<StartCombatResponse>(timeout.Token);
                if (!IsCurrentSession(session)) return false;
                if (CombatResult is not null) return synchronized = true;
                if (!IsCurrentSubscription(subscription) || !IsConnected) return false;
                if (result?.InitialState is null || result.CombatId != combatId) return false;
                // The payload of a SignalR notification is never applied. If another
                // notification arrived during this GET, obtain a fresh read before unlocking.
                if (_stateNotifications != notifications) continue;
                GameState = result.InitialState;
                RequiresSynchronization = !IsConnected;
                return synchronized = !RequiresSynchronization;
            }
            return false;
        }
        catch (Exception ex)
        {
            if (IsCurrentSession(session) && CombatResult is not null) return synchronized = true;
            if (!sessionCancellation.IsCancellationRequested && IsCurrentSubscription(subscription))
                _logger.LogWarning(ex, "Could not refresh combat state.");
            return false;
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                _synchronizationTask = null;
                // An interrupted subscription is not a failed read of the new one.
                // Drain this GET before scheduling a fresh read after JoinCombat.
                if (IsCurrentSubscription(subscription) && IsConnected)
                    _automaticSynchronizationBlocked = !synchronized;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
    }

    private async Task<bool> ConfirmNoActiveCombatAsync(long session, long? subscription = null)
    {
        var active = await CheckActiveCombatAsync();
        if (!IsCurrentSession(session)) return false;
        if (subscription.HasValue && (!IsCurrentSubscription(subscription.Value) || !IsConnected)) return false;
        if (active is not { HasActiveCombat: false }) return false;
        // Preserve the match type for the return destination. No result/rewards are inferred.
        IsCombatUnavailable = true;
        return true;
    }

    public async Task<bool> RefreshCombatAsync()
    {
        if (IsCommandPending || string.IsNullOrEmpty(CombatId)) return false;
        if (IsConnecting)
            return _synchronizationTask is { IsCompleted: false } ? await _synchronizationTask : false;
        var session = _sessionGeneration;
        IsConnecting = true;
        RequiresSynchronization = true;
        NotifyStateChanged();
        try
        {
            if (!IsConnected)
            {
                if (await ConfirmNoActiveCombatAsync(session)) return true;
                if (!IsCurrentSession(session)) return false;
                await ConnectToSignalRAsync(CombatId, session);
            }
            var refreshed = await SynchronizeAsync();
            if (refreshed && IsCurrentSession(session)) LastCommandResult = null;
            return refreshed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore combat connection.");
            return false;
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                IsConnecting = false;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
    }

    public async Task DisconnectAsync()
    {
        var connection = _hubConnection;
        var combatId = CombatId;
        var wasConnected = IsConnected;
        BeginSession();
        _hubConnection = null;
        _rejoinTask = null;
        _rejoiningCombatId = null;
        CombatResult = null;
        CombatId = null;
        MatchType = null;
        GameState = null;
        _battleLogs.Clear();
        IsConnecting = false;
        IsCommandPending = false;
        PendingCommandName = null;
        RequiresSynchronization = false;
        NotifyStateChanged();
        if (connection is not null)
        {
            try
            {
                if (wasConnected && !string.IsNullOrEmpty(combatId))
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await connection.InvokeAsync("LeaveCombat", combatId, timeout.Token);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not leave combat subscription before disconnecting."); }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _sessionCancellation.Dispose();
    }

    private async Task ConnectToSignalRAsync(string combatId, long session)
    {
        if (!IsCurrentSession(session)) throw new OperationCanceledException();
        var sessionCancellation = _sessionCancellation.Token;
        InvalidateSubscription();
        var apiBaseUrl = _http.BaseAddress?.ToString() ?? "";

        if (_hubConnection is not null)
        {
            var previous = _hubConnection;
            _hubConnection = null;
            await previous.DisposeAsync();
            if (!IsCurrentSession(session)) throw new OperationCanceledException();
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(apiBaseUrl + "hubs/combat")
            .WithAutomaticReconnect()
            .Build();

        connection.On<GameStateDto>("ReceiveGameStateUpdate", state =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection || CombatResult is not null || IsCombatUnavailable) return;
            _stateNotifications++;
            RequiresSynchronization = true;
            NotifyStateChanged();
            TrySynchronizeAutomatically();
        });

        connection.On<List<string>>("ReceiveBattleLogs", (logs) =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection) return;
            _battleLogs.AddRange(logs);
            NotifyStateChanged();
        });

        connection.On<CombatResultDto>("ReceiveCombatEnded", (result) =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection) return;
            CombatResult = result;
            IsCombatUnavailable = false;
            NotifyStateChanged();
        });

        connection.Reconnecting += _ =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection) return Task.CompletedTask;
            InvalidateSubscription();
            NotifyStateChanged();
            return Task.CompletedTask;
        };
        connection.Closed += _ =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection) return Task.CompletedTask;
            InvalidateSubscription();
            NotifyStateChanged();
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            if (!IsCurrentSession(session) || _hubConnection != connection) return;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var subscription = _subscriptionGeneration;
                await connection.InvokeAsync("JoinCombat", combatId, timeout.Token);
                if (!IsCurrentSession(session) || _hubConnection != connection || !IsCurrentSubscription(subscription)) return;
                ConfirmSubscription();
                // Uncertain commands require explicit recovery, even if the socket returns.
                if (!IsCommandPending && !_automaticSynchronizationBlocked && LastCommandResult?.Outcome != CombatCommandOutcome.Unconfirmed)
                    await SynchronizeAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not restore combat subscription."); }
            if (IsCurrentSession(session) && _hubConnection == connection) NotifyStateChanged();
        };


        try
        {
            _hubConnection = connection;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await connection.StartAsync(timeout.Token);
            var subscription = _subscriptionGeneration;
            await connection.InvokeAsync("JoinCombat", combatId, timeout.Token);
            if (!IsCurrentSession(session) || !IsCurrentSubscription(subscription)) throw new OperationCanceledException();
            ConfirmSubscription();
        }
        catch
        {
            if (IsCurrentSession(session) && _hubConnection == connection)
            {
                _hubConnection = null;
                _isSubscribed = false;
            }
            await connection.DisposeAsync();
            throw;
        }
    }

    public Task<CombatCommandResult> SurrenderAsync() => SendCommandAsync("Surrendering", "surrender", null);

    public async Task<ActiveCombatDto?> CheckActiveCombatAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await _http.GetAsync("api/combat/active", timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ActiveCombatDto>(timeout.Token);
                return result;
            }
        }
        catch { /* Ignora erros de rede na reconexão silenciosa */ }
        return null;
    }

    public Task<bool> RejoinCombatAsync(string combatId, MatchType? matchType)
    {
        // Route/authentication renders can request the same recovery concurrently.
        // Share it rather than disposing a connection that is still being started.
        if (_rejoinTask is { IsCompleted: false })
            return _rejoiningCombatId == combatId ? _rejoinTask : Task.FromResult(false);
        if (IsCommandPending || IsConnecting) return Task.FromResult(false);
        _rejoiningCombatId = combatId;
        return _rejoinTask = RejoinCombatCoreAsync(combatId, matchType);
    }

    private async Task<bool> RejoinCombatCoreAsync(string combatId, MatchType? matchType)
    {
        var session = BeginSession();
        IsConnecting = true;
        NotifyStateChanged();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_sessionCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await _http.GetAsync($"api/combat/{combatId}", timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartCombatResponse>(timeout.Token);
                if (!IsCurrentSession(session)) return false;
                if (result is { InitialState: not null } && !string.IsNullOrWhiteSpace(result.CombatId))
                {
                    CombatId = result.CombatId;
                    IsCombatUnavailable = false;
                    MatchType = matchType;
                    _battleLogs.Clear();
                    _battleLogs.AddRange(result.InitialLogs);
                    GameState = result.InitialState;

                    await ConnectToSignalRAsync(CombatId, session);
                    if (await SynchronizeAsync() && IsCurrentSession(session)) return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rejoin combat.");
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                IsConnecting = false;
                NotifyStateChanged();
                TrySynchronizeAutomatically();
            }
        }
        if (!IsCurrentSession(session)) return false;
        CombatId = null;
        MatchType = null;
        GameState = null;
        _battleLogs.Clear();
        NotifyStateChanged();
        return false;
    }

    private long BeginSession()
    {
        _sessionGeneration++;
        _sessionCancellation.Cancel();
        _sessionCancellation.Dispose();
        _sessionCancellation = new();
        _synchronizationTask = null;
        _stateNotifications = 0;
        _automaticSynchronizationBlocked = false;
        InvalidateSubscription();
        IsCombatUnavailable = false;
        LastCommandResult = null;
        RequiresSynchronization = true;
        return _sessionGeneration;
    }

    private bool IsCurrentSession(long session) => session == _sessionGeneration;
    private bool IsCurrentSubscription(long subscription) => subscription == _subscriptionGeneration;

    private void InvalidateSubscription()
    {
        _subscriptionGeneration++;
        _isSubscribed = false;
        RequiresSynchronization = true;
    }

    private void ConfirmSubscription()
    {
        // Reads started during the disconnected interval are invalid too.
        _subscriptionGeneration++;
        _isSubscribed = true;
        RequiresSynchronization = true;
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
