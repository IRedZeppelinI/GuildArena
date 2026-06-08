namespace GuildArena.Shared.DTOs.GuildAndHeroes;

/// <summary>
/// Data Transfer Object representing the player's guild overview, 
/// including progression and match statistics.
/// </summary>
public class GuildProfileDto
{
    public required string Name { get; set; }
    public int Level { get; set; }
    public int CurrentXP { get; set; }
    public int RequiredXP { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public Dictionary<string, int> DungeonCompletions { get; set; } = new();
}