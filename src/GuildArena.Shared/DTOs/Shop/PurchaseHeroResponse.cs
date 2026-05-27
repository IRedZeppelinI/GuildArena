namespace GuildArena.Shared.DTOs.Shop;

/// <summary>
/// Result of a hero purchase operation.
/// </summary>
public class PurchaseHeroResponse
{
    /// <summary>
    /// <c>true</c> if the purchase was successfully completed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error or informational message, empty on success.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The guild's gold balance after the purchase (only meaningful on success).
    /// </summary>
    public int UpdatedGold { get; set; }
}