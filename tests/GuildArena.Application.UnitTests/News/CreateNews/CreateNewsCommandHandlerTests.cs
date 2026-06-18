using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.News.CreateNews;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.News.CreateNews;

public class CreateNewsCommandHandlerTests
{
    private readonly INewsRepository _newsRepo;
    private readonly IStorageService _storageService;
    private readonly ILogger<CreateNewsCommandHandler> _logger;
    private readonly CreateNewsCommandHandler _handler;

    public CreateNewsCommandHandlerTests()
    {
        _newsRepo = Substitute.For<INewsRepository>();
        _storageService = Substitute.For<IStorageService>();
        _logger = Substitute.For<ILogger<CreateNewsCommandHandler>>();
        _handler = new CreateNewsCommandHandler(_newsRepo, _storageService, _logger);
    }

    [Fact]
    public async Task Handle_WithoutFile_ShouldCreateArticleWithoutImageAndReturnSuccess()
    {
        // Arrange
        var command = new CreateNewsCommand
        {
            Title = "Test Title",
            Summary = "Summary",
            Content = "Content"
        };
        var capturedArticle = default(NewsArticle);
        await _newsRepo.AddAsync(Arg.Do<NewsArticle>(a => capturedArticle = a), Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _newsRepo.Received(1).AddAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());

        capturedArticle.ShouldNotBeNull();
        capturedArticle.Title.ShouldBe(command.Title);
        capturedArticle.Summary.ShouldBe(command.Summary);
        capturedArticle.Content.ShouldBe(command.Content);
        capturedArticle.ImageUrl.ShouldBeNull();
        capturedArticle.IsPublished.ShouldBeTrue();
        capturedArticle.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(-5)); // recente
        _logger.Received(1).Log(
            LogLevel.Information,
            0,
            Arg.Is<object>(o => o.ToString()!.Contains(command.Title)),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_WithFile_ShouldUploadFileAndCreateArticleWithImageUrl()
    {
        // Arrange
        var command = new CreateNewsCommand
        {
            Title = "News with image",
            Summary = "Summary",
            Content = "Content",
            FileStream = new MemoryStream(new byte[] { 1, 2, 3 }),
            FileName = "cover.png",
            ContentType = "image/png"
        };
        var fakeUrl = "https://storage.example.com/images/cover.png";
        _storageService.UploadFileAsync(command.FileStream, command.FileName, command.ContentType, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fakeUrl));

        var capturedArticle = default(NewsArticle);
        await _newsRepo.AddAsync(Arg.Do<NewsArticle>(a => capturedArticle = a), Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _storageService.Received(1).UploadFileAsync(command.FileStream, command.FileName, command.ContentType, Arg.Any<CancellationToken>());
        capturedArticle.ShouldNotBeNull();
        capturedArticle.ImageUrl.ShouldBe(fakeUrl);
        capturedArticle.Title.ShouldBe(command.Title);
    }

    [Fact]
    public async Task Handle_WithFileStreamButEmptyFileName_ShouldNotUploadAndCreateArticleWithoutImage()
    {
        // Arrange
        var command = new CreateNewsCommand
        {
            Title = "No file name",
            Summary = "Summary",
            Content = "Content",
            FileStream = new MemoryStream(new byte[] { 1 }),
            FileName = "   " // whitespace
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        result.IsSuccess.ShouldBeTrue();
        await _newsRepo.Received(1).AddAsync(Arg.Is<NewsArticle>(a => a.ImageUrl == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        cts.Cancel();
        var command = new CreateNewsCommand
        {
            Title = "Test",
            Summary = "Summary",
            Content = "Content"
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(command, token));

        // Verify no downstream services were called
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _newsRepo.DidNotReceive().AddAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());
    }
}