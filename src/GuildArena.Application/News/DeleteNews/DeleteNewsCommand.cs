using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.News.DeleteNews;

public record DeleteNewsCommand(int Id) : IRequest<Result>;