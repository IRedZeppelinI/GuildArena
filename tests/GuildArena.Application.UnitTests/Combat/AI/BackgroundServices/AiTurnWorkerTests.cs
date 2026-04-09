using GuildArena.Application.Combat.AI;
using GuildArena.Application.Combat.AI.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.AI.BackgroundServices;

public class AiTurnWorkerTests
{
    private readonly IAiTurnQueue _queueMock;
    private readonly IServiceProvider _serviceProviderMock;
    private readonly IServiceScopeFactory _scopeFactoryMock;
    private readonly IServiceScope _scopeMock;
    private readonly IAiTurnOrchestrator _orchestratorMock;
    private readonly ILogger<AiTurnWorker> _loggerMock;
    private readonly AiTurnWorker _worker;

    public AiTurnWorkerTests()
    {
        _queueMock = Substitute.For<IAiTurnQueue>();
        _serviceProviderMock = Substitute.For<IServiceProvider>();
        _scopeFactoryMock = Substitute.For<IServiceScopeFactory>();
        _scopeMock = Substitute.For<IServiceScope>();
        _orchestratorMock = Substitute.For<IAiTurnOrchestrator>();
        _loggerMock = Substitute.For<ILogger<AiTurnWorker>>();

        // Setup the Dependency Injection Mocks to provide the Orchestrator
        _serviceProviderMock.GetService(typeof(IServiceScopeFactory)).Returns(_scopeFactoryMock);
        _scopeFactoryMock.CreateScope().Returns(_scopeMock);
        _scopeMock.ServiceProvider.Returns(_serviceProviderMock);
        _serviceProviderMock.GetService(typeof(IAiTurnOrchestrator)).Returns(_orchestratorMock);

        _worker = new AiTurnWorker(_queueMock, _serviceProviderMock, _loggerMock);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessTurn_WhenRequestIsDequeued()
    {
        // ARRANGE
        var request = new AiTurnRequest("Combat_123", 999);

        using var cts = new CancellationTokenSource();

        // Configuramos a fila para devolver um pedido na primeira vez que for chamada,
        // e na segunda vez lança o OperationCanceledException para simular o shutdown e parar o loop infinito.
        _queueMock.DequeueAsync(Arg.Any<CancellationToken>())
            .Returns(
                new ValueTask<AiTurnRequest>(request),
                new ValueTask<AiTurnRequest>(Task.FromException<AiTurnRequest>(new OperationCanceledException()))
            );

        // ACT
        // StartAsync é o método base que chama o ExecuteAsync em pano de fundo
        await _worker.StartAsync(cts.Token);

        // Damos um tempo minúsculo para o loop iterar pelo menos uma vez
        await Task.Delay(50);

        await _worker.StopAsync(cts.Token);

        // ASSERT
        // O Worker deve ter criado um Scope novo para proteger a memória
        _scopeFactoryMock.Received(1).CreateScope();

        // E deve ter passado o pedido ao Orquestrador
        await _orchestratorMock.Received(1).PlayTurnAsync("Combat_123", 999);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCrash_WhenOrchestratorThrowsException()
    {
        // ARRANGE
        var request = new AiTurnRequest("Combat_Crash", 111);
        using var cts = new CancellationTokenSource();

        _queueMock.DequeueAsync(Arg.Any<CancellationToken>())
            .Returns(
                new ValueTask<AiTurnRequest>(request),
                new ValueTask<AiTurnRequest>(Task.FromException<AiTurnRequest>(new OperationCanceledException()))
            );

        // Simulamos um erro grave na lógica de jogo da IA
        _orchestratorMock
            .When(x => x.PlayTurnAsync(Arg.Any<string>(), Arg.Any<int>()))
            .Throw(new Exception("Critical Game Logic Error"));

        // ACT
        await _worker.StartAsync(cts.Token);
        await Task.Delay(50); // Dar tempo ao Worker para processar
        await _worker.StopAsync(cts.Token);

        // ASSERT
        // O orquestrador foi chamado e rebentou
        await _orchestratorMock.Received(1).PlayTurnAsync("Combat_Crash", 111);

        // O Worker NÃO deve ter ido abaixo (o teste não daria fail).
        // Em vez disso, ele deve ter feito Log do erro e apanhado a exceção no try-catch interno.
        _loggerMock.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Error executing AI turn")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}