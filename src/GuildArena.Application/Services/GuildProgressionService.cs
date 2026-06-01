using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Services;

/// <inheritdoc />
public class GuildProgressionService : IGuildProgressionService
{
    private readonly ILogger<GuildProgressionService> _logger;

    public GuildProgressionService(ILogger<GuildProgressionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void AddXpAndLevelUpIfNeeded(Guild guild, int xpGained)
    {
        if (xpGained <= 0) return;

        guild.CurrentXP += xpGained;
        _logger.LogDebug("Guild {GuildId} gained {Xp} XP. Total XP: {CurrentXP}",
            guild.Id, xpGained, guild.CurrentXP);

        // Level up as long as current XP meets or exceeds the threshold for the current level
        while (guild.CurrentXP >= GetRequiredXpForLevel(guild.Level))
        {
            guild.CurrentXP -= GetRequiredXpForLevel(guild.Level);
            guild.Level++;
            _logger.LogInformation("Guild {GuildId} advanced to level {Level}!", guild.Id, guild.Level);
        }
    }

    /// <summary>
    /// Returns the total XP required to advance from the given level.
    /// Formula: Level * 1000.
    /// </summary>
    private static int GetRequiredXpForLevel(int level) => level * 1000;
}