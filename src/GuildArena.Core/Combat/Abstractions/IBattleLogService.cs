namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// A scoped service responsible for collecting narrative logs throughout the combat processing pipeline.
/// </summary>
public interface IBattleLogService
{
    /// <summary>
    /// Adds a message to the battle log.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Retrieves all logs collected so far and clears the internal buffer.
    /// Useful for flushing logs to the response at the end of a request.
    /// </summary>
    List<string> GetAndClearLogs();
}