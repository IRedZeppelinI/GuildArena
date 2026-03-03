namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Represents a combatant (Hero or Mob) on the battlefield from the client's perspective.
/// </summary>
public class CombatantDto
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public required string Name { get; set; }
    public required string RaceId { get; set; }

    public int MaxHP { get; set; }
    public int CurrentHP { get; set; }
    public bool IsAlive => CurrentHP > 0;

    public int ActionsTakenThisTurn { get; set; }

    /// <summary>
    /// Pre-calculated max actions available for this combatant to ease UI rendering.
    /// </summary>
    public int MaxActions { get; set; }

    public int Position { get; set; }

    public AbilitySummaryDto? SpecialAbility { get; set; }
    public List<AbilitySummaryDto> Abilities { get; set; } = new();

    /// <summary>
    /// Dictionary mapping AbilityId to the remaining turns on cooldown.
    /// </summary>
    public Dictionary<string, int> ActiveCooldowns { get; set; } = new();

    public List<ActiveModifierDto> ActiveModifiers { get; set; } = new();
}