namespace GuildArena.Domain.Entities;

public class HeroCharacter
{
    public int Id { get; set; }
    public int GuildId { get; set; }
    public required string CharacterDefinitionID { get; set; }

    public int CurrentLevel { get; set; }
    public int CurrentXP { get; set; }
    public int CurrentHP { get; set; }

    public List<string> UnlockedPerkIds { get; set; } = new();

}
