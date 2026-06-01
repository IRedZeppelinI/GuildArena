using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;

namespace GuildArena.Infrastructure.Persistence.Repositories;

/// <inheritdoc />
public class MatchRepository : IMatchRepository
{
    private readonly GuildArenaDbContext _context;

    public MatchRepository(GuildArenaDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task SaveMatchAsync(Match match, CancellationToken cancellationToken = default)
    {
        // Adding the root aggregate automatically brings
        // participants and hero entries into the change tracker
        _context.Matches.Add(match);
        await _context.SaveChangesAsync(cancellationToken);
    }
}