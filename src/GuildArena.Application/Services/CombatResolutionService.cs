using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Services;

/// <inheritdoc />
public class CombatResolutionService : ICombatResolutionService
{
    private readonly IEnumerable<IMatchTypeResolver> _resolvers;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ILogger<CombatResolutionService> _logger;

    public CombatResolutionService(
        IEnumerable<IMatchTypeResolver> resolvers,
        ICombatStateRepository combatStateRepo,
        ILogger<CombatResolutionService> logger)
    {
        _resolvers = resolvers;
        _combatStateRepo = combatStateRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CombatResultDto> ResolveCombatAsync(
        string combatId, GameState state, string userId, bool isSurrender, CancellationToken ct)
    {
        var resolver = _resolvers.FirstOrDefault(r => r.CanHandle(state.MatchType))
            ?? throw new InvalidOperationException($"No resolver found for match type {state.MatchType}");

        // Delegate all game-specific resolution to the appropriate resolver
        var resultDto = await resolver.ResolveMatchAsync(combatId, state, userId, isSurrender, ct);

        // Clean up Redis combat state
        await _combatStateRepo.DeleteAsync(combatId);

        var humanPlayers = state.Players.Where(p => p.Type == CombatPlayerType.Human);
        foreach (var player in humanPlayers)
        {
            if (!string.IsNullOrEmpty(player.UserId))
            {
                await _combatStateRepo.ClearPlayerActiveCombatAsync(player.UserId);
            }
        }

        _logger.LogInformation("Combat {CombatId} resolved. Type: {MatchType}", combatId, state.MatchType);

        return resultDto;
    }
}