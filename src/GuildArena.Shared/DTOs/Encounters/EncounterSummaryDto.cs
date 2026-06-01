namespace GuildArena.Shared.DTOs.Encounters;

/// <summary>
/// A lightweight representation of a PvE encounter to display in the world map or bounty board.
/// </summary>
public class EncounterSummaryDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int DifficultyRating { get; set; }
    public int RequiredGuildLevel { get; set; }
}