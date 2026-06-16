using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using MediatR;

namespace GuildArena.Application.News.GetNewsArticle;

public class GetNewsArticleQuery : IRequest<Result<NewsArticleDetailDto>>
{
    public int Id { get; set; }
}