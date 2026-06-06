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
/// Resolves Dungeon matches: manages the active dungeon run, stage progression,
/// hero persistence, and final completion rewards.
/// </summary>
public class DungeonMatchResolver : IMatchTypeResolver
{
    private readonly IGuildRepository _guildRepo;
    private readonly IDungeonRunRepository _dungeonRunRepo;
    private readonly IDungeonDefinitionRepository _dungeonDefRepo;
    private readonly IGuildProgressionService _progressionService;
    private readonly IMatchRepository _matchRepo;
    private readonly ILogger<DungeonMatchResolver> _logger;

    public DungeonMatchResolver(
    IGuildRepository guildRepo,
    IDungeonRunRepository dungeonRunRepo,
    IDungeonDefinitionRepository dungeonDefRepo,
    IGuildProgressionService progressionService,
    IMatchRepository matchRepo,
    ILogger<DungeonMatchResolver> logger)
    {
        _guildRepo = guildRepo;
        _dungeonRunRepo = dungeonRunRepo;
        _dungeonDefRepo = dungeonDefRepo;
        _progressionService = progressionService;
        _matchRepo = matchRepo;
        _logger = logger;
    }

    public bool CanHandle(MatchType matchType) => matchType == MatchType.Dungeon;

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

        var activeRun = await _dungeonRunRepo.GetActiveRunAsync(guild.Id, ct);
        if (activeRun == null)
            throw new InvalidOperationException("No active dungeon run found, but combat type is Dungeon.");

        // Load dungeon definition to fetch current stage details
        if (!_dungeonDefRepo.TryGetDefinition(activeRun.DungeonDefinitionId, out var dungeonDef))
            throw new InvalidOperationException($"Dungeon definition '{activeRun.DungeonDefinitionId}' not found.");

        var currentStage = dungeonDef.Stages.FirstOrDefault(s => s.StageIndex == activeRun.CurrentStageIndex);
        if (currentStage == null)
            throw new InvalidOperationException($"Current stage index {activeRun.CurrentStageIndex} not found in dungeon '{dungeonDef.Id}'.");

        int goldEarned = 0;
        int xpEarned = 0;
        bool leveledUp = false;

        if (!isWinner || isSurrender)
        {
            // Loss / Surrender: remove the run, small consolation
            await _dungeonRunRepo.DeleteRunAsync(activeRun, ct);
            _logger.LogInformation("Guild {GuildId} lost/surrendered dungeon {DungeonId}. Removing active run.", guild.Id, activeRun.DungeonDefinitionId);

            // Consolation rewards (flat, minimal)
            goldEarned = 20;
            xpEarned = 50;
        }
        else
        {
            // Victory: update hero HP states
            foreach (var heroState in activeRun.HeroesState)
            {
                var combatant = state.Combatants.FirstOrDefault(c => c.Id == heroState.HeroId);
                if (combatant != null)
                {
                    heroState.CurrentHP = combatant.CurrentHP;
                }
            }

            if (currentStage.IsBossNode)
            {
                // Final stage: complete dungeon
                guild.Wins++; // Track win for guild stats

                // Apply stage rewards (gold)
                goldEarned += currentStage.StageRewards.BaseGold;

                // Apply completion rewards (gold + XP)
                var completionRewards = dungeonDef.CompletionRewards;
                goldEarned += completionRewards.BaseGold;
                xpEarned += completionRewards.BaseGuildXp;

                guild.Gold += goldEarned;
                int previousLevel = guild.Level;
                _progressionService.AddXpAndLevelUpIfNeeded(guild, xpEarned);
                leveledUp = guild.Level > previousLevel;

                // Increment completion record
                await _dungeonRunRepo.IncrementDungeonRecordAsync(guild.Id, activeRun.DungeonDefinitionId, ct);

                // Save match history (human heroes only)
                var matchId = Guid.NewGuid();
                var match = new Match
                {
                    Id = matchId,
                    OccurredAt = DateTime.UtcNow,
                    Type = MatchType.Dungeon,
                    Participants = new List<MatchParticipant>
                    {
                        new MatchParticipant
                        {
                            Id = Guid.NewGuid(),
                            MatchId = matchId,
                            GuildId = guild.Id,
                            IsWinner = true,
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

                await _matchRepo.SaveMatchAsync(match, ct);

                // Delete the active run (dungeon finished)
                await _dungeonRunRepo.DeleteRunAsync(activeRun, ct);
                _logger.LogInformation("Guild {GuildId} completed dungeon {DungeonId}.", guild.Id, activeRun.DungeonDefinitionId);
            }
            else
            {
                // Non-boss stage: award stage gold, advance stage
                goldEarned += currentStage.StageRewards.BaseGold;
                guild.Gold += goldEarned;

                activeRun.CurrentStageIndex++;
                await _dungeonRunRepo.UpdateRunAsync(activeRun, ct);
                _logger.LogInformation("Guild {GuildId} cleared stage {StageIndex} of dungeon {DungeonId}. Advancing to next stage.",
                    guild.Id, currentStage.StageIndex, activeRun.DungeonDefinitionId);
            }
        }

        await _guildRepo.UpdateGuildAsync(guild);

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