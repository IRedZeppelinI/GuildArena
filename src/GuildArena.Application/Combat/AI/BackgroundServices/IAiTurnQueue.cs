namespace GuildArena.Application.Combat.AI.BackgroundServices;

/// <summary>
/// Defines a thread-safe queue for scheduling AI turns.
/// Acts as the communication channel between the HTTP request threads and the background worker.
/// </summary>
public interface IAiTurnQueue
{
    /// <summary>
    /// Adds an AI turn request to the processing queue.
    /// </summary>
    ValueTask EnqueueAsync(AiTurnRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the next AI turn request from the queue. 
    /// Yields asynchronously until an item is available.
    /// </summary>
    ValueTask<AiTurnRequest> DequeueAsync(CancellationToken cancellationToken = default);
}