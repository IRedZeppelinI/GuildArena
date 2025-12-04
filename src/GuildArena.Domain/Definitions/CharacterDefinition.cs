using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines the static template for a unique Hero Character (e.g., "Garrett", "Zog").
/// </summary>
public class CharacterDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    
    /// <summary>
    /// The ID of the race this character belongs to (e.g., "RACE_HUMAN").
    /// This applies racial modifiers and bonus stats automatically via the Factory.
    /// </summary>
    public required string RaceId { get; set; }

    // --- ESTATÍSTICAS ---
    /// <summary>
    /// The starting stats at Level 1 (before Race bonuses).
    /// </summary>
    public required BaseStats BaseStats { get; set; }

    /// <summary>
    /// The amount of stats gained per level up.
    /// </summary>
    public required BaseStats StatsGrowthPerLevel { get; set; }

    /// <summary>
    /// Base HP at Level 1 (before Constitution/Defense scaling).
    /// </summary>
    public int BaseHP { get; set; }

    /// <summary>
    /// HP gained per level (Flat).
    /// </summary>
    public int HPGrowthPerLevel { get; set; }

    // --- HABILIDADES (Loadout Base) ---

    public required string BasicAttackAbilityId { get; set; }

    // Habilidades de defesa/foco específicas deste herói 
    // (Se null, a Factory usará as defaults da Raça)
    public string? GuardAbilityId { get; set; }
    public string? FocusAbilityId { get; set; }

    /// <summary>
    /// The list of Ability IDs that this character knows by default.
    /// </summary>
    public List<string> SkillIds { get; set; } = new();
}