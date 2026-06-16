namespace GuildArena.Domain.Entities;

public class NewsArticle
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Content { get; set; }
    public string? ImageUrl { get; set; }

    // Será preenchido no CreateNewsCommandHandler.
    public DateTime CreatedAt { get; set; }

    public bool IsPublished { get; set; }
}