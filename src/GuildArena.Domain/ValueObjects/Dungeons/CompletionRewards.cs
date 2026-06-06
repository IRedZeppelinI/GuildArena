namespace GuildArena.Domain.ValueObjects.Dungeons;

/// <summary>
/// Rewards earned upon completing the entire dungeon (all stages).
/// </summary>
public class CompletionRewards
{
    public int BaseGuildXp { get; set; }
    public int BaseGold { get; set; }
    public List<string> RewardBannerIds { get; set; } = new();
}