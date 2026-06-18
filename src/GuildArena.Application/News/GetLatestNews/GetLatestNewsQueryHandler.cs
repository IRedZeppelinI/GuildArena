using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetLatestNews;

/// <summary>
/// Handles retrieval of the latest published news summaries.
/// </summary>
public class GetLatestNewsQueryHandler : IRequestHandler<GetLatestNewsQuery, Result<List<NewsSummaryDto>>>
{
    private readonly INewsRepository _newsRepo;

    public GetLatestNewsQueryHandler(INewsRepository newsRepo)
    {
        _newsRepo = newsRepo;
    }

    /// <summary>
    /// Fetches the latest published articles and maps them to <see cref="NewsSummaryDto"/> objects.
    /// </summary>
    /// <param name="request">The query specifying the maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful result containing the list of summaries, or an empty list if none exist.</returns>
    public async Task<Result<List<NewsSummaryDto>>> Handle(GetLatestNewsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var articles = await _newsRepo.GetLatestPublishedAsync(request.Limit, cancellationToken);

        var dtos = articles.Select(a => new NewsSummaryDto
        {
            Id = a.Id,
            Title = a.Title,
            Summary = a.Summary,
            ImageUrl = a.ImageUrl,
            CreatedAt = a.CreatedAt
        }).ToList();

        return dtos;
    }
}