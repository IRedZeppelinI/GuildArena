using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.News.DeleteNews;

/// <summary>
/// Handles the deletion of a news article by its identifier.
/// </summary>
public class DeleteNewsCommandHandler : IRequestHandler<DeleteNewsCommand, Result>
{
    private readonly INewsRepository _newsRepo;

    public DeleteNewsCommandHandler(INewsRepository newsRepo)
    {
        _newsRepo = newsRepo;
    }

    /// <summary>
    /// Deletes the specified news article if it exists.
    /// </summary>
    /// <param name="request">The command containing the article ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success, or a not-found error.</returns>
    public async Task<Result> Handle(DeleteNewsCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var article = await _newsRepo.GetPublishedByIdAsync(request.Id, ct);
        if (article == null)
            return Result.Failure(new Error("News.NotFound", "Article not found", ErrorType.NotFound));

        await _newsRepo.DeleteAsync(article, ct);
        return Result.Success();
    }
}