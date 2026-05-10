using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.DTOs.GuildAndHeroes;

/// <summary>
/// Data Transfer Object representing an ability's details for UI display.
/// </summary>
public class HeroAbilityDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ActionPointCost { get; set; }
    public int BaseCooldown { get; set; }
    public Dictionary<EssenceType, int> Costs { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}