using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GuildArena.Infrastructure.Persistence.Repositories;

public class NewsRepository : INewsRepository
{
    private readonly GuildArenaDbContext _dbContext;

    public NewsRepository(GuildArenaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NewsArticle article, CancellationToken ct = default)
    {
        _dbContext.NewsArticles.Add(article);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<NewsArticle>> GetLatestPublishedAsync(int limit, CancellationToken ct = default)
    {
        return await _dbContext.NewsArticles
            .AsNoTracking()
            .Where(a => a.IsPublished)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<NewsArticle?> GetPublishedByIdAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.NewsArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.IsPublished, ct);
    }
}