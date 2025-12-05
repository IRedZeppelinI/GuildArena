using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines a playable race, including base stats bonuses and racial traits (modifiers).
/// </summary>
public class RaceDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    
    /// <summary>
    /// List of modifier IDs that are permanently applied to characters of this race.
    /// </summary>
    public List<string> RacialModifierIds { get; set; } = new();

    /// <summary>
    /// Base stat bonuses granted by this race (added to Character base stats).
    /// Can include MaxHP bonuses.
    /// </summary>
    public BaseStats BonusStats { get; set; } = new();

    
}