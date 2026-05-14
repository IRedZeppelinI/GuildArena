namespace GuildArena.Shared.DTOs.GuildAndHeroes;

public class TraitDto
{
    public required string SourceName { get; set; }
    public bool IsRacial { get; set; }
    public required string Name { get; set; }

    public List<string> DescriptionLines { get; set; } = new();
}

