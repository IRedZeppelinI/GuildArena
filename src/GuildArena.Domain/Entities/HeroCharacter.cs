namespace GuildArena.Domain.Entities;

public class HeroCharacter
{
    public int Id { get; set; }
    public int GuildId { get; set; }
    public string CharacterDefinitionID { get; set; } = string.Empty;

    public int CurrentLevel { get; set; }
    public int CurrentXP { get; set; }
    public int CurrentHP { get; set; }

}
