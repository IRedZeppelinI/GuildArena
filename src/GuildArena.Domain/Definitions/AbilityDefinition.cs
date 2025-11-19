using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines the static properties and rules of an ability.
/// </summary>
public class AbilityDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    
    /// <summary>
    /// The list of essence costs required to activate this ability.
    /// </summary>
    public List<EssenceCost> Costs { get; set; } = new();
    public int HPCost { get; set; }
    public int BaseCooldown { get; set; }
    public List<TargetingRule> TargetingRules { get; set; } = new();
    public List<EffectDefinition> Effects { get; set; } = new();

    public List<string> Tags { get; set; } = new();
}


