using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.News.CreateNews;

/// <summary>
/// Handles the creation of a news article, optionally uploading an attached image.
/// </summary>
public class CreateNewsCommandHandler : IRequestHandler<CreateNewsCommand, Result>
{
    private readonly INewsRepository _newsRepo;
    private readonly IStorageService _storageService;
    private readonly ILogger<CreateNewsCommandHandler> _logger;

    public CreateNewsCommandHandler(
        INewsRepository newsRepo,
        IStorageService storageService,
        ILogger<CreateNewsCommandHandler> logger)
    {
        _newsRepo = newsRepo;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new news article, uploading the attached file if present, and persists it.
    /// </summary>
    /// <param name="request">The command containing title, summary, content and optional file data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    public async Task<Result> Handle(CreateNewsCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? imageUrl = null;

        if (request.FileStream is not null && !string.IsNullOrWhiteSpace(request.FileName))
        {
            imageUrl = await _storageService.UploadFileAsync(
                request.FileStream,
                request.FileName,
                request.ContentType ?? "application/octet-stream",
                cancellationToken);
        }

        var article = new NewsArticle
        {
            Title = request.Title,
            Summary = request.Summary,
            Content = request.Content,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow,
            IsPublished = true
        };

        await _newsRepo.AddAsync(article, cancellationToken);

        _logger.LogInformation("News article '{Title}' created with Id={Id}", article.Title, article.Id);
        return Result.Success();
    }
}