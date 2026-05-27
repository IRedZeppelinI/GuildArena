using System.Text.Json.Serialization;

namespace GuildArena.Domain.ValueObjects.UnlockHero;

/// <summary>
/// Aggregates the gold cost and conditions required to unlock a hero.
/// Deserialized from the character definition JSON.
/// </summary>
public class HeroUnlockRequirements
{
    /// <summary>
    /// The amount of gold required to purchase the hero.
    /// </summary>
    public int GoldCost { get; init; }

    /// <summary>
    /// The list of conditions that must be met (all evaluated as logical AND).
    /// If the list is empty, only the <see cref="GoldCost"/> restricts the purchase.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UnlockHeroCondition> Conditions { get; init; } = new();
}