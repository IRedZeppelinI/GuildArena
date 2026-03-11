using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.Requests;

/// <summary>
/// Represents a player's request to exchange two of their existing essences for one of their choice.
/// </summary>
public class ExchangeEssenceRequest
{
    public required string CombatId { get; set; }

    /// <summary>
    /// The essences the player is sacrificing. 
    /// The sum of all values in this dictionary MUST equal 2.
    /// </summary>
    public Dictionary<EssenceType, int> EssenceToSpend { get; set; } = new();

    /// <summary>
    /// The specific essence type the player wishes to receive (Quantity is always 1).
    /// </summary>
    public EssenceType EssenceToGain { get; set; }
}