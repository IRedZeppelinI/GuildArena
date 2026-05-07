namespace GuildArena.Shared.DTOs.GuildAndHeroes;

/// <summary>
/// Data Transfer Object representing a hero instance in a player's roster.
/// Includes dynamic state from the database and static data (Name) from definitions.
/// </summary>
public class HeroDto
{
    public int Id { get; set; }
    public required string DefinitionId { get; set; }
    public required string Name { get; set; }
    public int CurrentLevel { get; set; }
}