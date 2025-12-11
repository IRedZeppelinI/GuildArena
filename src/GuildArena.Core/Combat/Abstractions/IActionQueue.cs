namespace GuildArena.Core.Combat.Abstractions;

using GuildArena.Core.Combat.Actions;

/// <summary>
/// Represents a FIFO (First-In-First-Out) queue for scheduling combat actions.
/// Decouples the "Triggering" of an action from its "Execution", resolving circular dependencies.
/// </summary>
public interface IActionQueue
{
    /// <summary>
    /// Adds a new action to the end of the processing queue.
    /// </summary>
    /// <param name="action">The action to schedule.</param>
    void Enqueue(ICombatAction action);

    /// <summary>
    /// Removes and returns the action at the beginning of the queue.
    /// </summary>
    /// <returns>The next action to process, or null if empty.</returns>
    ICombatAction? Dequeue();

    /// <summary>
    /// Checks if there are any actions waiting in the queue.
    /// </summary>
    bool HasNext();

    /// <summary>
    /// Clears all pending actions from the queue.
    /// </summary>
    void Clear();
}