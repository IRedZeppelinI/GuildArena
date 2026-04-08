using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.AI.BackgroundServices;

public class AiTurnQueue : IAiTurnQueue
{
    private readonly Channel<AiTurnRequest> _queue;
    private readonly ILogger<AiTurnQueue> _logger;

    public AiTurnQueue(ILogger<AiTurnQueue> logger)
    {
        _logger = logger;

        // Bounded channel prevents memory exhaustion if the system gets flooded with requests.
        // It will hold up to 1000 pending AI turns before making producers wait.
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _queue = Channel.CreateBounded<AiTurnRequest>(options);
    }

    public async ValueTask EnqueueAsync(
        AiTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogDebug(
            "Enqueueing AI turn for Combat {CombatId}, Player {PlayerId}",
            request.CombatId,
            request.AiPlayerId);

        await _queue.Writer.WriteAsync(request, cancellationToken);
    }

    public async ValueTask<AiTurnRequest> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var request = await _queue.Reader.ReadAsync(cancellationToken);

        _logger.LogDebug(
            "Dequeued AI turn for Combat {CombatId}. Pending in queue: {Count}",
            request.CombatId,
            _queue.Reader.Count);

        return request;
    }
}