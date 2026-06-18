using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.News.UpdateNews;

/// <summary>
/// Handles updating an existing news article, allowing text and image modifications.
/// </summary>
public class UpdateNewsCommandHandler : IRequestHandler<UpdateNewsCommand, Result>
{
    private readonly INewsRepository _newsRepo;
    private readonly IStorageService _storageService;
    private readonly ILogger<UpdateNewsCommandHandler> _logger;

    public UpdateNewsCommandHandler(
        INewsRepository newsRepo,
        IStorageService storageService,
        ILogger<UpdateNewsCommandHandler> logger)
    {
        _newsRepo = newsRepo;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Updates the title, summary, content and optionally the image of an existing news article.
    /// </summary>
    /// <param name="request">The command containing the article ID and updated data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or a not-found error.</returns>
    public async Task<Result> Handle(UpdateNewsCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var article = await _newsRepo.GetPublishedByIdAsync(request.Id, cancellationToken);

        if (article == null)
        {
            return Result.Failure(new Error("News.NotFound", "Article not found.", ErrorType.NotFound));
        }

        // Atualizar textos
        article.Title = request.Title;
        article.Summary = request.Summary;
        article.Content = request.Content;

        // Se o admin enviou uma nova imagem, fazemos upload e substituímos o URL
        if (request.FileStream is not null && !string.IsNullOrWhiteSpace(request.FileName))
        {
            var newImageUrl = await _storageService.UploadFileAsync(
                request.FileStream,
                request.FileName,
                request.ContentType ?? "application/octet-stream",
                cancellationToken);

            article.ImageUrl = newImageUrl;
        }

        await _newsRepo.UpdateAsync(article, cancellationToken);

        _logger.LogInformation("News article '{Title}' updated with Id={Id}", article.Title, article.Id);
        return Result.Success();
    }
}