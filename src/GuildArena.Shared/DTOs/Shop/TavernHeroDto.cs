using GuildArena.Domain.Enums.UnlockHero;

namespace GuildArena.Shared.DTOs.Shop;

/// <summary>
/// Represents a hero entry in the tavern shop, combining static definition data,
/// purchase requirements, and the current ownership status for the requesting guild.
/// </summary>
public class TavernHeroDto
{
    /// <summary>
    /// The identifier of the Hero entity in the database, when the hero is already owned.
    /// If the hero is not yet owned, this value is <c>null</c>.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// The unique definition ID of the hero (e.g., "HERO_VEX").
    /// </summary>
    public string DefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the hero, from the static definition.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the hero's race, resolved from the RaceDefinition.
    /// </summary>
    public string RaceName { get; set; } = string.Empty;

    /// <summary>
    /// Amount of gold required to purchase the hero. Zero if already owned.
    /// </summary>
    public int GoldCost { get; set; }

    /// <summary>
    /// Current ownership status of this hero for the requesting guild.
    /// </summary>
    public HeroStatus Status { get; set; }

    /// <summary>
    /// List of unlock conditions with current progress.
    /// Empty when the hero is already owned or has no unlock requirements.
    /// </summary>
    public List<UnlockConditionDto> UnlockConditions { get; set; } = new();
}