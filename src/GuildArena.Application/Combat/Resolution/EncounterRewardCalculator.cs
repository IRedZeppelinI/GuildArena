using GuildArena.Application.Abstractions;
using GuildArena.Application.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Matches;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.Resolution;

/// <summary>
/// Calculates and applies rewards for PvE encounter matches.
/// </summary>
public class EncounterRewardCalculator : IMatchRewardCalculator
{
    private readonly IEncounterDefinitionRepository _encounterRepo;
    private readonly IGuildProgressionService _progressionService;
    private readonly ILogger<EncounterRewardCalculator> _logger;

    public EncounterRewardCalculator(
        IEncounterDefinitionRepository encounterRepo,
        IGuildProgressionService progressionService,
        ILogger<EncounterRewardCalculator> logger)
    {
        _encounterRepo = encounterRepo;
        _progressionService = progressionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanHandle(GuildArena.Domain.Enums.Matches.MatchType matchType) => matchType == GuildArena.Domain.Enums.Matches.MatchType.Encounter;

    /// <inheritdoc />
    public MatchRewardResult CalculateAndApplyRewards(GameState gameState, Guild playerGuild, bool isWinner)
    {
        // Retrieve the encounter definition using the context ID
        if (!_encounterRepo.TryGetDefinition(gameState.ContextId, out var encounterDef))
        {
            _logger.LogWarning("Encounter definition '{EncounterId}' not found for reward calculation.", gameState.ContextId);
            return new MatchRewardResult { XpEarned = 0, GoldEarned = 0, LeveledUp = false };
        }

        var rewards = encounterDef.Rewards;

        // Determine the multipliers based on victory or defeat
        float goldMultiplier = isWinner ? 1.0f : 0.1f;   // 10% gold for loss
        float xpMultiplier = isWinner ? 1.0f : 0.1f;     // 10% XP for loss

        int goldEarned = (int)(rewards.BaseGold * goldMultiplier);
        int xpEarned = (int)(rewards.BaseGuildXp * xpMultiplier);

        // Apply gold immediately
        playerGuild.Gold += goldEarned;

        // Apply XP and track level-up
        int previousLevel = playerGuild.Level;
        _progressionService.AddXpAndLevelUpIfNeeded(playerGuild, xpEarned);
        bool leveledUp = playerGuild.Level > previousLevel;

        _logger.LogInformation(
            "Encounter {EncounterId} reward applied. Winner: {IsWinner}. Gold: +{Gold}, XP: +{XP}. LeveledUp: {LeveledUp}",
            gameState.ContextId, isWinner, goldEarned, xpEarned, leveledUp);

        return new MatchRewardResult
        {
            GoldEarned = goldEarned,
            XpEarned = xpEarned,
            LeveledUp = leveledUp
        };
    }
}