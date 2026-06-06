using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;
using Microsoft.Extensions.Logging;
using MatchType = GuildArena.Domain.Enums.Matches.MatchType;

namespace GuildArena.Application.Combat.Resolution;

/// <summary>
/// Resolves PvE Encounter matches: calculates rewards, updates guild, and creates match history.
/// </summary>
public class EncounterMatchResolver : IMatchTypeResolver
{
    private readonly IGuildRepository _guildRepo;
    private readonly IEncounterDefinitionRepository _encounterRepo;
    private readonly IGuildProgressionService _progressionService;
    private readonly IMatchRepository _matchRepo;
    private readonly ILogger<EncounterMatchResolver> _logger;

    public EncounterMatchResolver(
    IGuildRepository guildRepo,
    IEncounterDefinitionRepository encounterRepo,
    IGuildProgressionService progressionService,
    IMatchRepository matchRepo,
    ILogger<EncounterMatchResolver> logger)
    {
        _guildRepo = guildRepo;
        _encounterRepo = encounterRepo;
        _progressionService = progressionService;
        _matchRepo = matchRepo;
        _logger = logger;
    }

    public bool CanHandle(MatchType matchType) => matchType == MatchType.Encounter;

    public async Task<CombatResultDto> ResolveMatchAsync(
    string combatId,
    GameState state,
    string userId,
    bool isSurrender,
    CancellationToken ct)
    {
        var guild = await _guildRepo.GetGuildWithHistoryAsync(userId)
        ?? throw new InvalidOperationException("Resolution: guild not found.");

        var player = state.Players.First(p => p.UserId == userId);
        int playerSeatId = player.PlayerId;

        bool isWinner = isSurrender ? false : DetermineIsWinner(state, playerSeatId);

        // Update guild stats
        if (isWinner) guild.Wins++;
        else guild.Losses++;

        // Apply rewards
        int goldEarned = 0, xpEarned = 0;
        bool leveledUp = false;

        if (_encounterRepo.TryGetDefinition(state.ContextId, out var encounterDef))
        {
            float multiplier = isWinner ? 1.0f : 0.1f;
            goldEarned = (int)(encounterDef.Rewards.BaseGold * multiplier);
            xpEarned = (int)(encounterDef.Rewards.BaseGuildXp * multiplier);

            guild.Gold += goldEarned;

            int previousLevel = guild.Level;
            _progressionService.AddXpAndLevelUpIfNeeded(guild, xpEarned);
            leveledUp = guild.Level > previousLevel;
        }

        // Build match history (human only)
        var matchId = Guid.NewGuid();
        var match = new Match
        {
            Id = matchId,
            OccurredAt = DateTime.UtcNow,
            Type = MatchType.Encounter,
            Participants = new List<MatchParticipant>
            {
                new MatchParticipant
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    GuildId = guild.Id,
                    IsWinner = isWinner,
                    HeroesUsed = state.Combatants
                        .Where(c => c.OwnerId == playerSeatId)
                        .Select(c => new MatchHeroEntry
                        {
                            Id = Guid.NewGuid(),
                            HeroDefinitionId = c.DefinitionId,
                            LevelSnapshot = c.Level
                        })
                        .ToList()
                }
            }
        };

        await _guildRepo.UpdateGuildAsync(guild);
        await _matchRepo.SaveMatchAsync(match, ct);

        _logger.LogInformation("Resolved Encounter. Winner: {IsWinner}, XP: {XP}, Gold: {Gold}", isWinner, xpEarned, goldEarned);

        int newGuildLevel = leveledUp ? guild.Level : 0;
        return new CombatResultDto
        {
            IsWinner = isWinner,
            XpGained = xpEarned,
            GoldGained = goldEarned,
            NewGuildLevel = newGuildLevel,
            IsSurrender = isSurrender
        };
    }

    private static bool DetermineIsWinner(GameState state, int playerSeatId)
    {
        if (state.Players.Count > 0 && playerSeatId == state.Players[0].PlayerId)
            return state.Status == CombatStatus.Player1Won;
        if (state.Players.Count > 1 && playerSeatId == state.Players[1].PlayerId)
            return state.Status == CombatStatus.Player2Won;
        return false;
    }
}