using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetLatestNews;

public class GetLatestNewsQueryHandler : IRequestHandler<GetLatestNewsQuery, Result<List<NewsSummaryDto>>>
{
    private readonly INewsRepository _newsRepo;

    public GetLatestNewsQueryHandler(INewsRepository newsRepo)
    {
        _newsRepo = newsRepo;
    }

    public async Task<Result<List<NewsSummaryDto>>> Handle(GetLatestNewsQuery request, CancellationToken cancellationToken)
    {
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