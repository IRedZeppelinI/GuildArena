using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.Responses;
using GuildArena.Web.State;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System.Net;
using System.Text.Json;
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
    public async Task StartPveCombatAsync_ShouldSetConnectingState_AndInvokeOnChange()
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
        await _service.StartPveCombatAsync("ENC_1", new List<int> { 1 });

        // ASSERT
        // O evento deve ser disparado 2 vezes (1 quando começa a ligar, 1 quando termina)
        eventFiredCount.ShouldBeGreaterThanOrEqualTo(2);

        // No final, o estado deve estar atualizado
        _service.IsConnecting.ShouldBeFalse();
        _service.CombatId.ShouldBe("C1");
        _service.BattleLogs.ShouldContain("Combat Started");
        _service.GameState.ShouldNotBeNull();
    }

    [Fact]
    public async Task StartPveCombatAsync_OnHttpError_ShouldNotSetState_AndLogWarning()
    {
        // ARRANGE
        // A API devolve 400 Bad Request
        _mockHttpHandler.SetResponse(HttpStatusCode.BadRequest, "Invalid encounter");

        // ACT
        await _service.StartPveCombatAsync("ENC_FAIL", new List<int> { 1 });

        // ASSERT
        _service.CombatId.ShouldBeNull();
        _service.GameState.ShouldBeNull();
        _service.IsConnecting.ShouldBeFalse();

        // Verifica se fez log do erro
        _loggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Failed to start combat")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ==========================================================
    // HELPER CLASS: Um Fake HttpMessageHandler para testes de UI
    // ==========================================================
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode;
        private string _content = string.Empty;

        public void SetResponse(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = _statusCode,
                Content = new StringContent(_content)
            };
            return Task.FromResult(response);
        }
    }
}