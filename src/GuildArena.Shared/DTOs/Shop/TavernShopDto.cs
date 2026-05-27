namespace GuildArena.Shared.DTOs.Shop;

/// <summary>
/// Full state of the tavern shop returned to the client.
/// Contains the guild's current gold and the list of all available heroes
/// with their statuses and unlock progress.
/// </summary>
public class TavernShopDto
{
    /// <summary>
    /// Current gold balance of the authenticated guild.
    /// </summary>
    public int GuildGold { get; set; }

    /// <summary>
    /// List of all heroes in the game, each annotated with ownership status and conditions.
    /// </summary>
    public List<TavernHeroDto> Heroes { get; set; } = new();
}