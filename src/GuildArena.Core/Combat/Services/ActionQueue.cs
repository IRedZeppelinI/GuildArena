using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Services;

/// <summary>
/// In-memory implementation of the Action Queue.
/// Should be registered as Scoped per request/match to maintain isolation.
/// </summary>
public class ActionQueue : IActionQueue
{
    private readonly Queue<ICombatAction> _queue = new();
    private readonly ILogger<ActionQueue> _logger;

    public ActionQueue(ILogger<ActionQueue> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void Enqueue(ICombatAction action)
    {
        if (action == null)
        {
            _logger.LogWarning("Attempted to enqueue a null action.");
            return;
        }

        _queue.Enqueue(action);
        _logger.LogDebug("Action Enqueued: {ActionName} (Source: {SourceId}). Queue Size: {Count}",
            action.Name, action.Source?.Id, _queue.Count);
    }

    /// <inheritdoc />
    public ICombatAction? Dequeue()
    {
        if (_queue.Count == 0) return null;

        var action = _queue.Dequeue();
        // Log de debug para seguirmos o fluxo
        _logger.LogDebug(
            "Action Dequeued: {ActionName}. Remaining: {Count}", action.Name, _queue.Count);
        return action;
    }

    /// <inheritdoc />
    public bool HasNext()
    {
        return _queue.Count > 0;
    }

    /// <inheritdoc />
    public void Clear()
    {
        int count = _queue.Count;
        _queue.Clear();
        _logger.LogInformation("Action Queue cleared. Removed {Count} pending actions.", count);
    }
}