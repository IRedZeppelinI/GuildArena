namespace GuildArena.Shared.DTOs.News;

/// <summary>
/// Identifiers and public SPA paths for the built-in news illustrations.
/// </summary>
public static class NewsIllustrations
{
    public const string Dungeon = "dungeon";
    public const string Essence = "essence";
    public const string Hero = "hero";

    public static string? GetImageUrl(string? identifier) => identifier switch
    {
        Dungeon => "/images/public/news-dungeon.jpg",
        Essence => "/images/public/news-essence.jpg",
        Hero => "/images/public/news-hero.jpg",
        _ => null
    };
}
