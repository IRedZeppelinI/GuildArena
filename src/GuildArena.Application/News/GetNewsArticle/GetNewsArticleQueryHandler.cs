using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetNewsArticle;

public class GetNewsArticleQueryHandler : IRequestHandler<GetNewsArticleQuery, Result<NewsArticleDetailDto>>
{
    private readonly INewsRepository _newsRepo;

    public GetNewsArticleQueryHandler(INewsRepository newsRepo)
    {
        _newsRepo = newsRepo;
    }

    public async Task<Result<NewsArticleDetailDto>> Handle(GetNewsArticleQuery request, CancellationToken cancellationToken)
    {
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