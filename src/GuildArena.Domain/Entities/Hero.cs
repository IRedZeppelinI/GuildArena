namespace GuildArena.Domain.Entities;

public class Hero
{
    public int Id { get; set; }

    public int GuildId { get; set; }
    public Guild? Guild { get; set; }

    public required string CharacterDefinitionId { get; set; }

    public int CurrentLevel { get; set; }
    public int CurrentXP { get; set; }

}


