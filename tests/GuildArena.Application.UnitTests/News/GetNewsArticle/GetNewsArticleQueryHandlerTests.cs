using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.News.GetNewsArticle;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.News;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.News.GetNewsArticle;

public class GetNewsArticleQueryHandlerTests
{
    private readonly INewsRepository _newsRepo;
    private readonly GetNewsArticleQueryHandler _handler;

    public GetNewsArticleQueryHandlerTests()
    {
        _newsRepo = Substitute.For<INewsRepository>();
        _handler = new GetNewsArticleQueryHandler(_newsRepo);
    }

    [Fact]
    public async Task Handle_WhenArticleExists_ShouldReturnMappedDetailDto()
    {
        // Arrange
        var article = new NewsArticle
        {
            Id = 42,
            Title = "Breaking News",
            Summary = "Summary text",
            Content = "Full content",
            ImageUrl = "images/42.png",
            CreatedAt = new DateTime(2025, 1, 15)
        };
        _newsRepo.GetPublishedByIdAsync(42, Arg.Any<CancellationToken>()).Returns(article);
        var query = new GetNewsArticleQuery { Id = 42 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value;
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(42);
        dto.Title.ShouldBe("Breaking News");
        dto.Summary.ShouldBe("Summary text");
        dto.Content.ShouldBe("Full content");
        dto.ImageUrl.ShouldBe("images/42.png");
        dto.CreatedAt.ShouldBe(new DateTime(2025, 1, 15));
    }

    [Fact]
    public async Task Handle_WhenArticleNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        _newsRepo.GetPublishedByIdAsync(999, Arg.Any<CancellationToken>()).Returns((NewsArticle?)null);
        var query = new GetNewsArticleQuery { Id = 999 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe("News.NotFound");
        result.Error.Message.ShouldBe("Article not found");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        cts.Cancel();
        var query = new GetNewsArticleQuery { Id = 1 };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(query, token));
        await _newsRepo.DidNotReceive().GetPublishedByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}