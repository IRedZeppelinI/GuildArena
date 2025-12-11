namespace GuildArena.Core.Combat.Actions;

/// <summary>
/// Represents the outcome of a processed combat action.
/// Acts as a container for narrative logs (Battle Logs) to be displayed in the UI,
/// as well as execution status.
/// </summary>
public class CombatActionResult
{
    /// <summary>
    /// Indicates if the action was performed successfully without critical system errors.
    /// Default is true.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// A list of localized, formatted strings describing the events of this action for the player.
    /// These are intended to be displayed in the client's Battle Log UI.
    /// </summary>
    public List<string> BattleLogEntries { get; set; } = new();

    /// <summary>
    /// Optional: Semantic tags triggered during this action (e.g., "Critical", "Miss", "StunApplied").
    /// Useful for the frontend to play specific sound effects or animations.
    /// </summary>
    public HashSet<string> ResultTags { get; set; } = new();

    /// <summary>
    /// Helper method to add a UI log entry (Battle Log).
    /// </summary>
    public void AddBattleLog(string message)
    {
        BattleLogEntries.Add(message);
    }
}