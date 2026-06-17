using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.News.UpdateNews;

public class UpdateNewsCommand : IRequest<Result>
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Content { get; set; }
    public Stream? FileStream { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
}