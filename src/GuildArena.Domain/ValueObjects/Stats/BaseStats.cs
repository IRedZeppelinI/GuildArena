namespace GuildArena.Domain.ValueObjects.Stats;

public class BaseStats
{
    public float Attack { get; set; }
    public float Defense { get; set; }
    public float Agility { get; set; }
    public float Magic { get; set; }
    public float MagicDefense { get; set; }

    /// <summary>
    /// The base number of actions allowed per turn. Default is 1.
    /// </summary>
    public float MaxActions { get; set; } = 1;

    /// <summary>
    /// The base Maximum HP. 
    /// Used in Character Definitions (Base), Race Definitions (Bonus), and Growth.
    /// </summary>
    public float MaxHP { get; set; }
}
