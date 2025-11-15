namespace GuildArena.Domain.Enums;

public enum TargetType
{
    // --- Single Targets ---

    /// <summary>
    /// The caster of the ability.
    /// </summary>
    Self,

    /// <summary>
    /// A single friendly combatant (cannot be Self).
    /// </summary>
    Ally,

    /// <summary>
    /// A single friendly combatant (can be Self OR an Ally).
    /// </summary>
    Friendly,

    /// <summary>
    /// A single enemy combatant.
    /// </summary>
    Enemy,

    // --- Multiple Targets / AoE ---

    /// <summary>
    /// All friendly combatants (does NOT include Self).
    /// </summary>
    AllAllies,

    /// <summary>
    /// All friendly combatants (DOES include Self).
    /// </summary>
    AllFriendlies,

    /// <summary>
    /// All enemy combatants.
    /// </summary>
    AllEnemies
}