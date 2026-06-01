using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions.Repositories;

/// <summary>
/// Contract for persisting match data.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Saves a complete match record, including participants and heroes used.
    /// </summary>
    /// <param name="match">The match aggregate to persist.</param>
    Task SaveMatchAsync(Match match, CancellationToken cancellationToken = default);
}