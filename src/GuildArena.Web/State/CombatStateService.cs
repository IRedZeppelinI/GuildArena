using GuildArena.Domain.Enums.Resources;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.Requests;
using GuildArena.Shared.Responses;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace GuildArena.Web.State;

public class CombatStateService : ICombatStateService, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<CombatStateService> _logger;
    private HubConnection? _hubConnection;
    private readonly List<string> _battleLogs = new();

    public event Action? OnChange;

    public string? CombatId { get; private set; }
    public GameStateDto? GameState { get; private set; }
    public IReadOnlyList<string> BattleLogs => _battleLogs.AsReadOnly();
    public bool IsConnecting { get; private set; }

    public CombatStateService(HttpClient http, ILogger<CombatStateService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task StartPveCombatAsync(string encounterId, List<int> heroInstanceIds)
    {
        IsConnecting = true;
        NotifyStateChanged();

        try
        {
            var request = new StartPveRequest
            {
                EncounterId = encounterId,
                HeroInstanceIds = heroInstanceIds
            };

            var response = await _http.PostAsJsonAsync("api/combat/start-pve", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<StartCombatResponse>();
                if (result != null)
                {
                    CombatId = result.CombatId;
                    _battleLogs.AddRange(result.InitialLogs);
                    GameState = result.InitialState;

                    await ConnectToSignalRAsync(CombatId);
                }
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to start combat. API returned {StatusCode}: {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while starting the combat session.");
        }
        finally
        {
            IsConnecting = false;
            NotifyStateChanged();
        }
    }

    public async Task EndTurnAsync()
    {
        if (string.IsNullOrEmpty(CombatId)) return;

        try
        {
            var response = await _http.PostAsync($"api/combat/{CombatId}/end-turn", null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("End turn request failed. API returned {StatusCode}: {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while ending the turn.");
        }
    }

    public async Task ExecuteAbilityAsync(
        int sourceId,
        string abilityId,
        Dictionary<string, List<int>> targetSelections,
        Dictionary<EssenceType, int> payment)
    {
        if (string.IsNullOrEmpty(CombatId)) return;

        var request = new ExecuteAbilityRequest
        {
            CombatId = CombatId,
            SourceId = sourceId,
            AbilityId = abilityId,
            TargetSelections = targetSelections,
            Payment = payment
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"api/combat/{CombatId}/execute-ability", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Ability execution failed. API returned {StatusCode}: {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred executing the ability.");
        }
    }

    public async Task ExchangeEssenceAsync(Dictionary<EssenceType, int> spent, EssenceType gained)
    {
        if (string.IsNullOrEmpty(CombatId)) return;

        var request = new ExchangeEssenceRequest
        {
            CombatId = CombatId,
            EssenceToSpend = spent,
            EssenceToGain = gained
        };

        try
        {
            var response = await _http.PostAsJsonAsync($"api/combat/{CombatId}/exchange-essence", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Exchange request failed. API returned {StatusCode}: {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred exchanging essence.");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection is not null)
        {
            if (!string.IsNullOrEmpty(CombatId))
            {
                await _hubConnection.InvokeAsync("LeaveCombat", CombatId);
            }
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }

        CombatId = null;
        GameState = null;
        _battleLogs.Clear();
        IsConnecting = false;
        NotifyStateChanged();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private async Task ConnectToSignalRAsync(string combatId)
    {
        var apiBaseUrl = _http.BaseAddress?.ToString() ?? "";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(apiBaseUrl + "hubs/combat")
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<GameStateDto>("ReceiveGameStateUpdate", (state) =>
        {
            GameState = state;
            NotifyStateChanged();
        });

        _hubConnection.On<List<string>>("ReceiveBattleLogs", (logs) =>
        {
            _battleLogs.AddRange(logs);
            NotifyStateChanged();
        });

        await _hubConnection.StartAsync();
        await _hubConnection.InvokeAsync("JoinCombat", combatId);
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}