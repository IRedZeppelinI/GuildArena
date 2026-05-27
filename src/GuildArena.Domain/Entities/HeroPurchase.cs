namespace GuildArena.Domain.Entities;

/// <summary>
/// Records the unlock transaction of a hero for a specific guild.
/// A hero can only be unlocked once per guild.
/// </summary>
public class HeroPurchase
{
    /// <summary>
    /// Unique identifier of the purchase record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The guild that made the purchase.
    /// </summary>
    public int GuildId { get; set; }

    /// <summary>
    /// Navigation property to the guild.
    /// </summary>
    public Guild Guild { get; set; } = null!;

    /// <summary>
    /// The identifier of the unlocked character definition.
    /// </summary>
    public string CharacterDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Amount of gold paid for the unlock.
    /// </summary>
    public int GoldPaid { get; set; }

    /// <summary>
    /// UTC timestamp of the purchase.
    /// </summary>
    public DateTime PurchasedAt { get; set; }
}