using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.News.DeleteNews;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.News.DeleteNews;

public class DeleteNewsCommandHandlerTests
{
    private readonly INewsRepository _newsRepo;
    private readonly DeleteNewsCommandHandler _handler;

    public DeleteNewsCommandHandlerTests()
    {
        _newsRepo = Substitute.For<INewsRepository>();
        _handler = new DeleteNewsCommandHandler(_newsRepo);
    }

    [Fact]
    public async Task Handle_WhenArticleExists_ShouldDeleteAndReturnSuccess()
    {
        // Arrange
        var article = new NewsArticle
        {
            Id = 1,
            Title = "Test",
            Summary = "Summary",       // required
            Content = "Content"        // required
        };
        _newsRepo.GetPublishedByIdAsync(1, Arg.Any<CancellationToken>()).Returns(article);
        var command = new DeleteNewsCommand(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _newsRepo.Received(1).DeleteAsync(article, Arg.Any<CancellationToken>());
        await _newsRepo.Received(1).GetPublishedByIdAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenArticleNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        _newsRepo.GetPublishedByIdAsync(99, Arg.Any<CancellationToken>()).Returns((NewsArticle?)null);
        var command = new DeleteNewsCommand(99);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe("News.NotFound");
        result.Error.Message.ShouldBe("Article not found");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        await _newsRepo.DidNotReceive().DeleteAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Handle_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        cts.Cancel();
        var command = new DeleteNewsCommand(1);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(command, token));

        // Verify repository was never called
        await _newsRepo.DidNotReceive().GetPublishedByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _newsRepo.DidNotReceive().DeleteAsync(Arg.Any<NewsArticle>(), Arg.Any<CancellationToken>());
    }
}