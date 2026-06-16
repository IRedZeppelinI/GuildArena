using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions.Repositories;

public interface INewsRepository
{
    Task AddAsync(NewsArticle article, CancellationToken ct = default);
    Task<List<NewsArticle>> GetLatestPublishedAsync(int limit, CancellationToken ct = default);
    Task<NewsArticle?> GetPublishedByIdAsync(int id, CancellationToken ct = default);
}