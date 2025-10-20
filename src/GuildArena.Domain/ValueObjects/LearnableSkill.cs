namespace GuildArena.Domain.ValueObjects;

public class LearnableSkill
{
    public int LevelRequired { get; set; }
    public required string AbilityID { get; set; }
}
