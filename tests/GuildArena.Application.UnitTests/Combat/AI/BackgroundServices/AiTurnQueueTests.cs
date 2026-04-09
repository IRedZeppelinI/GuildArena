using GuildArena.Application.Combat.AI.BackgroundServices;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Combat.AI.BackgroundServices;

public class AiTurnQueueTests
{
    private readonly ILogger<AiTurnQueue> _loggerMock;
    private readonly AiTurnQueue _queue;

    public AiTurnQueueTests()
    {
        _loggerMock = Substitute.For<ILogger<AiTurnQueue>>();
        _queue = new AiTurnQueue(_loggerMock);
    }
    [Fact]
    public async Task EnqueueAsync_ShouldThrowArgumentNullException_WhenRequestIsNull()
    {
        // ACT & ASSERT
        await Should.ThrowAsync<ArgumentNullException>(() =>
            _queue.EnqueueAsync(null!).AsTask());
    }

    [Fact]
    public async Task EnqueueAndDequeue_ShouldProcessInFifoOrder()
    {
        // ARRANGE
        var request1 = new AiTurnRequest("Combat_1", 999);
        var request2 = new AiTurnRequest("Combat_2", 888);

        // ACT
        await _queue.EnqueueAsync(request1);
        await _queue.EnqueueAsync(request2);

        var result1 = await _queue.DequeueAsync();
        var result2 = await _queue.DequeueAsync();

        // ASSERT
        result1.ShouldNotBeNull();
        result1.CombatId.ShouldBe("Combat_1");
        result1.AiPlayerId.ShouldBe(999);

        result2.ShouldNotBeNull();
        result2.CombatId.ShouldBe("Combat_2");
        result2.AiPlayerId.ShouldBe(888);
    }

    [Fact]
    public async Task DequeueAsync_ShouldRespectCancellationToken()
    {
        // ARRANGE
        // Criamos um token que é cancelado instantaneamente
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // ACT & ASSERT
        // Tentar ler de uma fila vazia com um token cancelado deve lançar OperationCanceledException
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _queue.DequeueAsync(cts.Token).AsTask());
    }
}