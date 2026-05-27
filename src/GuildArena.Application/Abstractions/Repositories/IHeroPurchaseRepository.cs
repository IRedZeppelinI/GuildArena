using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions.Repositories;

/// <summary>
/// Repository for hero unlock purchase records.
/// </summary>
public interface IHeroPurchaseRepository
{
    /// <summary>
    /// Persists a new purchase record.
    /// </summary>
    Task AddAsync(HeroPurchase purchase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a guild has already unlocked a specific hero.
    /// </summary>
    //removido temporariamente. GuildRooster já mostra os desbloqueados
    // Task<bool> IsHeroUnlockedByGuildAsync(int guildId, string characterDefinitionId, CancellationToken cancellationToken = default);
}