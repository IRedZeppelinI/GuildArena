using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetNewsArticle;

/// <summary>
/// Handles retrieval of a single published news article by its identifier.
/// </summary>
public class GetNewsArticleQueryHandler : IRequestHandler<GetNewsArticleQuery, Result<NewsArticleDetailDto>>
{
    private readonly INewsRepository _newsRepo;

    public GetNewsArticleQueryHandler(INewsRepository newsRepo)
    {
        _newsRepo = newsRepo;
    }

    /// <summary>
    /// Fetches a published article and maps it to a <see cref="NewsArticleDetailDto"/>.
    /// </summary>
    /// <param name="request">The query containing the article ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful result with the article detail, or a not-found failure.</returns>
    public async Task<Result<NewsArticleDetailDto>> Handle(GetNewsArticleQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var article = await _newsRepo.GetPublishedByIdAsync(request.Id, cancellationToken);

        if (article is null)
        {
            return Result.Failure<NewsArticleDetailDto>(new Error(
                "News.NotFound", "Article not found", ErrorType.NotFound));
        }

        return new NewsArticleDetailDto
        {
            Id = article.Id,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            ImageUrl = article.ImageUrl,
            CreatedAt = article.CreatedAt
        };
    }
}