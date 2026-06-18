using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.News.UpdateNews;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.News.UpdateNews;

public class UpdateNewsCommandHandlerTests
{
    private readonly INewsRepository _newsRepo;
    private readonly IStorageService _storageService;
    private readonly ILogger<UpdateNewsCommandHandler> _logger;
    private readonly UpdateNewsCommandHandler _handler;

    public UpdateNewsCommandHandlerTests()
    {
        _newsRepo = Substitute.For<INewsRepository>();
        _storageService = Substitute.For<IStorageService>();
        _logger = Substitute.For<ILogger<UpdateNewsCommandHandler>>();
        _handler = new UpdateNewsCommandHandler(_newsRepo, _storageService, _logger);
    }

    [Fact]
    public async Task Handle_WhenArticleExistsAndNoNewFile_ShouldUpdateTextsAndReturnSuccess()
    {
        // Arrange
        var article = new NewsArticle
        {
            Id = 10,
            Title = "Old Title",
            Summary = "Old Summary",
            Content = "Old Content",
            ImageUrl = "old.png"
        };
        _newsRepo.GetPublishedByIdAsync(10, Arg.Any<CancellationToken>()).Returns(article);
        var command = new UpdateNewsCommand
        {
            Id = 10,
            Title = "New Title",
            Summary = "New Summary",
            Content = "New Content"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        article.Title.ShouldBe("New Title");
        article.Summary.ShouldBe("New Summary");
        article.Content.ShouldBe("New Content");
        article.ImageUrl.ShouldBe("old.png"); // unchanged
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _newsRepo.Received(1).UpdateAsync(article, Arg.Any<CancellationToken>());
        _logger.Received(1).Log(
            LogLevel.Information,
            0,
            Arg.Is<object>(o => o.ToString()!.Contains("New Title")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_WhenArticleExistsAndNewFileProvided_ShouldUploadAndUpdateImageUrl()
    {
        // Arrange
        var article = new NewsArticle
        {
            Id = 10,
            Title = "Title",
            Summary = "Sum",
            Content = "Cont",
            ImageUrl = "old.png"
        };
        _newsRepo.GetPublishedByIdAsync(10, Arg.Any<CancellationToken>()).Returns(article);
        var newImageUrl = "https://storage.example.com/new.png";
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateNewsCommand
        {
            Id = 10,
            Title = "Title",
            Summary = "Sum",
            Content = "Cont",
            FileStream = stream,
            FileName = "new.png",
            ContentType = "image/png"
        };
        _storageService.UploadFileAsync(stream, "new.png", "image/png", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(newImageUrl));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        article.ImageUrl.ShouldBe(newImageUrl);
        await _storageService.Received(1).UploadFileAsync(stream, "new.png", "image/png", Arg.Any<CancellationToken>());
        await _newsRepo.Received(1).UpdateAsync(article, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenArticleNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        _newsRepo.GetPublishedByIdAsync(999, Arg.Any<CancellationToken>()).Returns((NewsArticle?)null);
        var command = new UpdateNewsCommand
        {
            Id = 999,
            Title = "Title",
            Summary = "Sum",
            Content = "Cont"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe("News.NotFound");
        result.Error.Message.ShouldBe("Article not found.");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _newsRepo.DidNotReceive().UpdateAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        cts.Cancel();
        var command = new UpdateNewsCommand
        {
            Id = 1,
            Title = "Test",
            Summary = "Test",
            Content = "Test"
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(command, token));
        await _newsRepo.DidNotReceive().GetPublishedByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _storageService.DidNotReceive().UploadFileAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _newsRepo.DidNotReceive().UpdateAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());
    }
}