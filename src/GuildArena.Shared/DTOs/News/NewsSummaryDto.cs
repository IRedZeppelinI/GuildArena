namespace GuildArena.Shared.DTOs.News;

public class NewsSummaryDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}