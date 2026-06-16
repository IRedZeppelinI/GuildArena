using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetLatestNews;

public class GetLatestNewsQuery : IRequest<Result<List<NewsSummaryDto>>>
{
    public int Limit { get; set; } = 5;
}