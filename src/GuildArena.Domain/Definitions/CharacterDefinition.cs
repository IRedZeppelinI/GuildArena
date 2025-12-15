using GuildArena.Domain.ValueObjects.Stats;

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
    /// Includes Attack, Defense, MaxHP, etc.
    /// </summary>
    public required BaseStats BaseStats { get; set; }

    /// <summary>
    /// The amount of stats gained per level up.
    /// Includes HP growth via the MaxHP property.
    /// </summary>
    public required BaseStats StatsGrowthPerLevel { get; set; }



    // --- HABILIDADES (Loadout Base) ---
    /// <summary>
    /// The ID of a specific modifier (Trait) unique to this character.
    /// Applied automatically at the start of combat.
    /// </summary>
    public string? TraitModifierId { get; set; } 


    public required string BasicAttackAbilityId { get; set; }

    // Habilidades de defesa/foco específicas deste herói    
    public string? GuardAbilityId { get; set; }
    public string? FocusAbilityId { get; set; }

    /// <summary>
    /// The list of Ability IDs that this character knows by default.
    /// </summary>
    public List<string> AbilityIds { get; set; } = new();
}