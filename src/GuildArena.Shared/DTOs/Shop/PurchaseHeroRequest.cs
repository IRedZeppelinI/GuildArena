namespace GuildArena.Shared.DTOs.Shop;

/// <summary>
/// Request body for purchasing a hero from the tavern.
/// The server identifies the guild from the authenticated user's identity.
/// </summary>
public class PurchaseHeroRequest
{
    /// <summary>
    /// The definition ID of the hero to purchase (e.g., "HERO_VEX").
    /// </summary>
    public string HeroId { get; set; } = string.Empty;
}