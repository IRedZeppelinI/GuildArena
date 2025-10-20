using GuildArena.Domain.ValueObjects;

namespace GuildArena.Domain.Definitions;

public class CharacterDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public BaseStats Stats { get; set; }
    public BaseStats StatsGrowthPerLevel { get; set; }

    public List<LearnableSkill> SkillTree { get; set; }
}
