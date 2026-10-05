using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.Responses;
using GuildArena.Web.State;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text.Json;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;
using Xunit;

namespace GuildArena.Web.UnitTests.State;

public class CombatStateServiceTests
{
    private readonly ILogger<CombatStateService> _loggerMock;
    private readonly MockHttpMessageHandler _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly CombatStateService _service;

    public CombatStateServiceTests()
    {
        _loggerMock = Substitute.For<ILogger<CombatStateService>>();

        // O HttpClient nativo não tem interface, por isso criamos um "Falso Motor de HTTP"
        _mockHttpHandler = new MockHttpMessageHandler();

        _httpClient = new HttpClient(_mockHttpHandler)
        {
            BaseAddress = new Uri("https://localhost/")
        };

        _service = new CombatStateService(_httpClient, _loggerMock);
    }

    [Fact]
    public async Task StartPveCombatAsync_WhenHubUnavailable_ShouldRequireRecovery()
    {
        // ARRANGE
        // Preparamos a resposta falsa da API (200 OK com o nosso JSON)
        var fakeResponse = new StartCombatResponse
        {
            CombatId = "C1",
            InitialLogs = new List<string> { "Combat Started" },
            InitialState = new GameStateDto()
        };

        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(fakeResponse));

        // Vamos contar quantas vezes o evento OnChange é disparado
        int eventFiredCount = 0;
        _service.OnChange += () => eventFiredCount++;

        // ACT
        var result = await _service.StartEncounterCombatAsync("ENC_1", new List<int> { 1 });

        // ASSERT
        // O evento deve ser disparado 2 vezes (1 quando começa a ligar, 1 quando termina)
        eventFiredCount.ShouldBeGreaterThanOrEqualTo(2);

        // No final, o estado deve estar atualizado
        _service.IsConnecting.ShouldBeFalse();
        _service.CombatId.ShouldBe("C1");
        _service.MatchType.ShouldBe(MatchType.Encounter);
        _service.BattleLogs.ShouldContain("Combat Started");
        _service.GameState.ShouldNotBeNull();
        result.ShouldBe(EncounterStartResult.Unconfirmed);
    }

    [Fact]
    public async Task StartPveCombatAsync_OnHttpError_ShouldNotSetState_AndLogWarning()
    {
        // ARRANGE
        // A API devolve 400 Bad Request
        _mockHttpHandler.SetResponse(HttpStatusCode.BadRequest, "Invalid encounter");

        // ACT
        var result = await _service.StartEncounterCombatAsync("ENC_FAIL", new List<int> { 1 });

        // ASSERT
        _service.CombatId.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.IsConnecting.ShouldBeFalse();
        result.ShouldBe(EncounterStartResult.Rejected);

        // Verifica se fez log do erro
        _loggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to start combat")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartPveCombatAsync_WhenResponseIsLost_ShouldNotReportRejection()
    {
        _mockHttpHandler.FailRequest();

        var result = await _service.StartEncounterCombatAsync("ENC_1", new List<int> { 1, 2, 3 });

        result.ShouldBe(EncounterStartResult.Unconfirmed);
        _service.CombatId.ShouldBeNull();
        _service.IsConnecting.ShouldBeFalse();
    }

    [Fact]
    public async Task RejoinCombatAsync_WhenHubUnavailable_ShouldNotRetainIncompleteCombat()
    {
        var fakeResponse = new StartCombatResponse
        {
            CombatId = "C1",
            InitialLogs = new List<string> { "Combat Started" },
            InitialState = new GameStateDto()
        };
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(fakeResponse));

        var rejoined = await _service.RejoinCombatAsync("C1", MatchType.Dungeon);

        rejoined.ShouldBeFalse();
        _service.CombatId.ShouldBeNull();
        _service.MatchType.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.BattleLogs.ShouldBeEmpty();
        _service.IsConnecting.ShouldBeFalse();
    }

    [Fact]
    public async Task EnterDungeonCombatAsync_OnRejectedRequest_ShouldNotReportReady()
    {
        _mockHttpHandler.SetResponse(HttpStatusCode.Conflict, "Active combat exists");

        var result = await _service.EnterDungeonCombatAsync();

        result.ShouldBe(EncounterStartResult.Rejected);
        _service.CombatId.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.IsConnecting.ShouldBeFalse();
    }

    [Fact]
    public async Task EnterDungeonCombatAsync_WhenResponseIsLost_ShouldRequireRecovery()
    {
        _mockHttpHandler.FailRequest();

        var result = await _service.EnterDungeonCombatAsync();

        result.ShouldBe(EncounterStartResult.Unconfirmed);
        _service.CombatId.ShouldBeNull();
        _service.IsConnecting.ShouldBeFalse();
    }

    [Fact]
    public async Task EnterDungeonCombatAsync_WhenHubUnavailable_ShouldRequireRecovery()
    {
        var response = new StartCombatResponse
        {
            CombatId = "C1",
            InitialLogs = new List<string> { "Stage entered" },
            InitialState = new GameStateDto()
        };
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));

        var result = await _service.EnterDungeonCombatAsync();

        result.ShouldBe(EncounterStartResult.Unconfirmed);
        _service.MatchType.ShouldBe(MatchType.Dungeon);
        _service.IsConnecting.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_ClearsMatchTypeBeforeAnotherCombat()
    {
        var response = new StartCombatResponse
        {
            CombatId = "C1",
            InitialLogs = new List<string>(),
            InitialState = new GameStateDto()
        };
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(response));
        await _service.EnterDungeonCombatAsync();
        _service.MatchType.ShouldBe(MatchType.Dungeon);

        await _service.DisconnectAsync();

        _service.MatchType.ShouldBeNull();
        _service.CombatId.ShouldBeNull();
    }

    [Fact]
    public async Task CommandsWithoutAConnectedCombatAreUnavailableAndDoNotSendRequests()
    {
        (await _service.EndTurnAsync()).Outcome.ShouldBe(CombatCommandOutcome.Unavailable);
        (await _service.ExecuteAbilityAsync(1, "GUARD", new(), new())).Outcome.ShouldBe(CombatCommandOutcome.Unavailable);
        (await _service.ExchangeEssenceAsync(new(), GuildArena.Domain.Enums.Resources.EssenceType.Mind)).Outcome.ShouldBe(CombatCommandOutcome.Unavailable);
        (await _service.SurrenderAsync()).Outcome.ShouldBe(CombatCommandOutcome.Unavailable);
        _mockHttpHandler.RequestCount.ShouldBe(0);
        _service.IsCommandPending.ShouldBeFalse();
    }

    [Fact]
    public async Task RecoveryWithoutACombatDoesNotCreateOrRepeatAnyCommand()
    {
        (await _service.RefreshCombatAsync()).ShouldBeFalse();
        _mockHttpHandler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentRejoinRequestsShareOneRecovery()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockHttpHandler.DelayUntil = release.Task;
        _mockHttpHandler.SetResponse(HttpStatusCode.NotFound, "Expired combat");

        var first = _service.RejoinCombatAsync("C1", MatchType.Encounter);
        var second = _service.RejoinCombatAsync("C1", MatchType.Encounter);

        second.ShouldBeSameAs(first);
        _mockHttpHandler.RequestCount.ShouldBe(1);
        (await _service.RejoinCombatAsync("C2", MatchType.Dungeon)).ShouldBeFalse();
        release.SetResult();
        (await first).ShouldBeFalse();
        _service.IsConnecting.ShouldBeFalse();
    }

    [Theory]
    [InlineData(MatchType.Encounter)]
    [InlineData(MatchType.Dungeon)]
    public async Task RecoveryWhenServerConfirmsNoActiveCombatPreservesReturnContextWithoutInventingResult(MatchType matchType)
    {
        await PrepareDisconnectedCombatAsync(matchType);
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new ActiveCombatDto()));

        (await _service.RefreshCombatAsync()).ShouldBeTrue();

        _service.IsCombatUnavailable.ShouldBeTrue();
        _service.CombatId.ShouldBe("C1");
        _service.MatchType.ShouldBe(matchType);
        _service.CombatResult.ShouldBeNull();
        _service.RequiresSynchronization.ShouldBeTrue();
        (await _service.EndTurnAsync()).Outcome.ShouldBe(CombatCommandOutcome.Unavailable);

        await _service.DisconnectAsync();
        _service.IsCombatUnavailable.ShouldBeFalse();
    }

    [Fact]
    public async Task RecoveryWhenActiveLookupFailsDoesNotClaimCombatHasEnded()
    {
        await PrepareDisconnectedCombatAsync(MatchType.Dungeon);
        _mockHttpHandler.SetResponse(HttpStatusCode.ServiceUnavailable, "{}");

        (await _service.RefreshCombatAsync()).ShouldBeFalse();

        _service.IsCombatUnavailable.ShouldBeFalse();
        _service.RequiresSynchronization.ShouldBeTrue();
        _service.CombatId.ShouldBe("C1");
        _service.MatchType.ShouldBe(MatchType.Dungeon);
        _service.CombatResult.ShouldBeNull();
    }

    private async Task PrepareDisconnectedCombatAsync(MatchType matchType)
    {
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new StartCombatResponse
        {
            CombatId = "C1", InitialState = new GameStateDto(), InitialLogs = []
        }));
        var result = matchType == MatchType.Dungeon
            ? await _service.EnterDungeonCombatAsync()
            : await _service.StartEncounterCombatAsync("ENC_1", [1, 2, 3]);
        result.ShouldBe(EncounterStartResult.Unconfirmed);
        _service.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task DelayedRejoinCannotRestoreADisconnectedSession()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new StartCombatResponse
        {
            CombatId = "C1", InitialState = new GameStateDto(), InitialLogs = ["Old combat"]
        }));
        _mockHttpHandler.DelayUntil = release.Task;
        _mockHttpHandler.IgnoreCancellation = true;
        var recovery = _service.RejoinCombatAsync("C1", MatchType.Dungeon);

        await _service.DisconnectAsync();
        release.SetResult();

        (await recovery).ShouldBeFalse();
        _service.CombatId.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.BattleLogs.ShouldBeEmpty();
        _service.IsConnecting.ShouldBeFalse();
        _service.RequiresSynchronization.ShouldBeFalse();
    }

    [Fact]
    public async Task OldRecoveryCannotClearAnotherSessionEvenIfCombatIdsMatch()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new StartCombatResponse
        {
            CombatId = "C1", InitialState = new GameStateDto { CurrentTurnNumber = 1 }, InitialLogs = ["Old recovery"]
        }));
        _mockHttpHandler.DelayUntil = release.Task;
        _mockHttpHandler.IgnoreCancellation = true;
        var recovery = _service.RejoinCombatAsync("C1", MatchType.Encounter);

        await _service.DisconnectAsync();
        _mockHttpHandler.DelayUntil = null;
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new StartCombatResponse
        {
            CombatId = "C1", InitialState = new GameStateDto { CurrentTurnNumber = 9 }, InitialLogs = ["New session"]
        }));
        (await _service.EnterDungeonCombatAsync()).ShouldBe(EncounterStartResult.Unconfirmed);
        release.SetResult();

        (await recovery).ShouldBeFalse();
        _service.CombatId.ShouldBe("C1");
        _service.MatchType.ShouldBe(MatchType.Dungeon);
        _service.GameState!.CurrentTurnNumber.ShouldBe(9);
        _service.BattleLogs.ShouldBe(["New session"]);
        _service.IsConnecting.ShouldBeFalse();
        _service.RequiresSynchronization.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedStartResponseCannotRepopulateStateAfterDisconnect(bool dungeon)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockHttpHandler.SetResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new StartCombatResponse
        {
            CombatId = "C1", InitialState = new GameStateDto(), InitialLogs = ["Old start"]
        }));
        _mockHttpHandler.DelayUntil = release.Task;
        var start = dungeon ? _service.EnterDungeonCombatAsync() : _service.StartEncounterCombatAsync("ENC_1", [1, 2, 3]);

        await _service.DisconnectAsync();
        release.SetResult();

        (await start).ShouldBe(EncounterStartResult.Unconfirmed);
        _service.CombatId.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.BattleLogs.ShouldBeEmpty();
        _service.IsConnecting.ShouldBeFalse();
        _service.RequiresSynchronization.ShouldBeFalse();
    }

    // ==========================================================
    // HELPER CLASS: Um Fake HttpMessageHandler para testes de UI
    // ==========================================================
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode;
        private string _content = string.Empty;
        private bool _failRequest;
        public int RequestCount { get; private set; }
        public Task? DelayUntil { get; set; }
        public bool IgnoreCancellation { get; set; }

        public void FailRequest() => _failRequest = true;

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var statusCode = _statusCode;
            var content = _content;
            if (DelayUntil is not null)
            {
                if (IgnoreCancellation) await DelayUntil;
                else await DelayUntil.WaitAsync(cancellationToken);
            }
            if (_failRequest) throw new HttpRequestException("Connection lost");
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            };
            return response;
        }
    }
}
