using GuildArena.Domain.Enums;

namespace GuildArena.Domain.ValueObjects.Modifiers;

/// <summary>
/// Defines the properties of a protective barrier provided by a modifier, including scaling rules.
/// </summary>
public class BarrierProperties
{
    /// <summary>
    /// The base amount of damage this barrier can absorb before scaling.
    /// </summary>
    public float BaseAmount { get; set; }

    /// <summary>
    /// The stat of the caster used to scale the barrier's strength.
    /// </summary>
    public StatType ScalingStat { get; set; }

    /// <summary>
    /// The multiplier applied to the scaling stat.
    /// </summary>
    public float ScalingFactor { get; set; }

    /// <summary>
    /// The list of tags that this barrier can absorb.
    /// If empty, it absorbs ALL damage types (generic barrier).
    /// </summary>
    public List<string> BlockedTags { get; set; } = new();
}