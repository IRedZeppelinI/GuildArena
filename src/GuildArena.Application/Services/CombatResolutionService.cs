using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Combat.Resolution;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Services;

/// <inheritdoc />
public class CombatResolutionService : ICombatResolutionService
{
    private readonly IEnumerable<IMatchRewardCalculator> _rewardCalculators;
    private readonly IMatchRepository _matchRepo;
    private readonly IGuildRepository _guildRepo;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ILogger<CombatResolutionService> _logger;

    public CombatResolutionService(
        IEnumerable<IMatchRewardCalculator> rewardCalculators,
        IMatchRepository matchRepo,
        IGuildRepository guildRepo,
        ICombatStateRepository combatStateRepo,
        ILogger<CombatResolutionService> logger)
    {
        _rewardCalculators = rewardCalculators;
        _matchRepo = matchRepo;
        _guildRepo = guildRepo;
        _combatStateRepo = combatStateRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CombatResultDto> ResolveCombatAsync(string combatId, GameState state, string userId, bool isSurrender, CancellationToken ct)
    {
        // ----- 1. Identify the player and guild -----
        var player = state.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null)
            throw new InvalidOperationException("Resolution: player not found in combat.");

        var guild = await _guildRepo.GetGuildWithHistoryAsync(userId);
        if (guild == null)
            throw new InvalidOperationException("Resolution: guild not found for the player.");

        int playerSeatId = player.PlayerId;

        // ----- 2. Determine victory -----
        bool isWinner = DetermineIsWinner(state, playerSeatId);

        // ----- 3. Apply rewards via strategy -----
        var calculator = _rewardCalculators.FirstOrDefault(c => c.CanHandle(state.MatchType));
        MatchRewardResult rewardResult = calculator?.CalculateAndApplyRewards(state, guild, isWinner)
            ?? new MatchRewardResult(); // no rewards if no calculator

        // ----- 4. Build Match history -----
        var match = new Match
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Type = state.MatchType
        };

        // Player participant
        var playerParticipant = new MatchParticipant
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            GuildId = guild.Id,
            IsWinner = isWinner
        };

        foreach (var combatant in state.Combatants.Where(c => c.OwnerId == playerSeatId))
        {
            playerParticipant.HeroesUsed.Add(new MatchHeroEntry
            {
                Id = Guid.NewGuid(),
                MatchParticipantId = playerParticipant.Id,
                HeroDefinitionId = combatant.DefinitionId,
                LevelSnapshot = combatant.Level
            });
        }

        // Opponent participant (AI for now)
        int opponentSeatId = state.Players.First(p => p.PlayerId != playerSeatId).PlayerId;
        var opponentParticipant = new MatchParticipant
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            GuildId = null,          // AI
            IsWinner = !isWinner
        };

        foreach (var combatant in state.Combatants.Where(c => c.OwnerId == opponentSeatId))
        {
            opponentParticipant.HeroesUsed.Add(new MatchHeroEntry
            {
                Id = Guid.NewGuid(),
                MatchParticipantId = opponentParticipant.Id,
                HeroDefinitionId = combatant.DefinitionId,
                LevelSnapshot = combatant.Level
            });
        }

        match.Participants.Add(playerParticipant);
        match.Participants.Add(opponentParticipant);

        // ----- 5. Persist everything -----
        await _guildRepo.UpdateGuildAsync(guild);
        await _matchRepo.SaveMatchAsync(match, ct);
        await _combatStateRepo.DeleteAsync(combatId);

        // ----- 6. Build result DTO -----
        return new CombatResultDto
        {
            IsWinner = isWinner,
            XpGained = rewardResult.XpEarned,
            GoldGained = rewardResult.GoldEarned,
            NewGuildLevel = guild.Level,
            IsSurrender = isSurrender
        };
    }

    /// <summary>
    /// Dynamically determines if the given seat ID is the winner based on combat status.
    /// </summary>
    private static bool DetermineIsWinner(GameState state, int playerSeatId)
    {
        if (state.Players.Count > 0 && playerSeatId == state.Players[0].PlayerId)
            return state.Status == CombatStatus.Player1Won;

        if (state.Players.Count > 1 && playerSeatId == state.Players[1].PlayerId)
            return state.Status == CombatStatus.Player2Won;

        return false; // draw or unknown
    }
}