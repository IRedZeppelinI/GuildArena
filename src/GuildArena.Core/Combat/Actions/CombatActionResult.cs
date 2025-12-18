namespace GuildArena.Core.Combat.Actions;


public class CombatActionResult
{
    /// <summary>
    /// Indicates if the action was performed successfully without critical system errors.
    /// Default is true.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Semantic tags triggered during this action (e.g., "Critical", "Miss", "StunApplied").
    /// Used by the Frontend to trigger animations/sounds.
    /// </summary>
    public HashSet<string> ResultTags { get; set; } = new();
}