using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Shared.DTOs.GuildAndHeroes;

/// <summary>
/// Comprehensive Data Transfer Object representing a hero's full profile, 
/// including dynamically calculated stats and detailed ability information.
/// </summary>
public class HeroDetailsDto
{
    public int Id { get; set; }
    public required string DefinitionId { get; set; }
    public required string Name { get; set; }
    public required string RaceName { get; set; }
    public int CurrentLevel { get; set; }

    // Computed Stats
    public int MaxHP { get; set; }
    public int MaxActions { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Agility { get; set; }
    public int Magic { get; set; }
    public int MagicDefense { get; set; }

    //public List<HeroAbilityDto> Abilities { get; set; } = new();
    public List<AbilitySummaryDto> Abilities { get; set; } = new();
    public List<TraitDto> Traits { get; set; } = new();
}