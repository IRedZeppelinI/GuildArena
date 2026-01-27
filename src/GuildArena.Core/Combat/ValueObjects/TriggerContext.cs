using GuildArena.Domain.Gameplay;

namespace GuildArena.Core.Combat.ValueObjects;

/// <summary>
/// Encapsulates the necessary context data required to evaluate and process modifier triggers.
/// Acts as a snapshot of an event within the combat loop.
/// </summary>
public class TriggerContext
{
    /// <summary>
    /// The combatant who initiated the action causing the trigger (e.g., the attacker).
    /// </summary>
    public required Combatant Source { get; init; }

    /// <summary>
    /// The combatant affected by the action (e.g., the target receiving damage), if applicable.
    /// Can be null for global or self-inflicted events (e.g., Start of Turn).
    /// </summary>
    public Combatant? Target { get; init; }

    /// <summary>
    /// The current state of the combat world, allowing access to other entities and global variables.
    /// </summary>
    public required GameState GameState { get; init; }

    /// <summary>
    /// A numeric value associated with the event, such as damage dealt or healing received.
    /// Used for scaling triggers (e.g., "Reflect 10% of damage taken").
    /// </summary>
    public float? Value { get; init; }

    /// <summary>
    /// A set of tags describing the nature of the event (e.g., "Melee", "Fire", "Void").
    /// Used to match specific trigger conditions.
    /// </summary>
    public HashSet<string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}