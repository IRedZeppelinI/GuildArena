namespace GuildArena.Domain.Enums.Stats;

public enum StatType
{
    None = 0,
    Attack,
    Defense,
    Agility,
    Magic,
    MagicDefense,
    /// <summary>
    /// Determines the maximum number of Action Points available per turn.
    /// </summary>
    MaxActions,
    /// <summary>
    /// Determines the Maximum Health Points of the combatant.
    /// Acts as a stat that can be scaled by level, traits, and buffs.
    /// </summary>
    MaxHP
}
