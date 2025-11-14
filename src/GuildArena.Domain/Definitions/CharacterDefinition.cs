using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

public class CharacterDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public required BaseStats Stats { get; set; }
    public required BaseStats StatsGrowthPerLevel { get; set; }

    public string? BasicAttackAbilityId { get; set; }

    public List<LearnableSkill> SkillTree { get; set; } = new();
}
