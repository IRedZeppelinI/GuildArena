namespace GuildArena.Shared.DTOs.GuildAndHeroes;

/// <summary>
/// Data Transfer Object representing a hero instance in a player's roster.
/// </summary>
public class HeroDto
{
    public int Id { get; set; }
    public required string DefinitionId { get; set; }
    public int CurrentLevel { get; set; }
}
