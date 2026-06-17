using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.News.DeleteNews;

public class DeleteNewsCommandHandler : IRequestHandler<DeleteNewsCommand, Result>
{
    private readonly INewsRepository _newsRepo;
    public DeleteNewsCommandHandler(INewsRepository newsRepo) => _newsRepo = newsRepo;

    public async Task<Result> Handle(DeleteNewsCommand request, CancellationToken ct)
    {
        // Como o GetPublishedById filtra IsPublished, fazemos uma query direta para apagar qualquer uma
        var article = await _newsRepo.GetPublishedByIdAsync(request.Id, ct);
        if (article == null) return Result.Failure(new Error("News.NotFound", "Article not found", ErrorType.NotFound));

        await _newsRepo.DeleteAsync(article, ct);
        return Result.Success();
    }
}