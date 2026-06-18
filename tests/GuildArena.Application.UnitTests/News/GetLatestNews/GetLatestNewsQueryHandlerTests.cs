using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.News.GetLatestNews;
using GuildArena.Domain.Entities;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.News.GetLatestNews;

public class GetLatestNewsQueryHandlerTests
{
    private readonly INewsRepository _newsRepo;
    private readonly GetLatestNewsQueryHandler _handler;

    public GetLatestNewsQueryHandlerTests()
    {
        _newsRepo = Substitute.For<INewsRepository>();
        _handler = new GetLatestNewsQueryHandler(_newsRepo);
    }

    [Fact]
    public async Task Handle_WhenArticlesExist_ShouldReturnMappedDtosWithSuccess()
    {
        // Arrange
        var articles = new List<NewsArticle>
        {
            new()
            {
                Id = 1,
                Title = "First",
                Summary = "Summary 1",
                Content = "Content 1",       // required
                ImageUrl = "img1.png",
                CreatedAt = new DateTime(2025, 1, 10)
            },
            new()
            {
                Id = 2,
                Title = "Second",
                Summary = "Summary 2",
                Content = "Content 2",
                ImageUrl = null,
                CreatedAt = new DateTime(2025, 1, 12)
            }
        };
        _newsRepo.GetLatestPublishedAsync(5, Arg.Any<CancellationToken>()).Returns(articles);
        var query = new GetLatestNewsQuery { Limit = 5 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var dtos = result.Value;
        dtos.Count.ShouldBe(2);

        dtos[0].Id.ShouldBe(1);
        dtos[0].Title.ShouldBe("First");
        dtos[0].Summary.ShouldBe("Summary 1");
        dtos[0].ImageUrl.ShouldBe("img1.png");
        dtos[0].CreatedAt.ShouldBe(new DateTime(2025, 1, 10));

        dtos[1].Id.ShouldBe(2);
        dtos[1].ImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_WhenNoArticles_ShouldReturnEmptyListWithSuccess()
    {
        // Arrange
        _newsRepo.GetLatestPublishedAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<NewsArticle>());
        var query = new GetLatestNewsQuery { Limit = 10 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPassLimitToRepository()
    {
        // Arrange
        var query = new GetLatestNewsQuery { Limit = 7 };
        _newsRepo.GetLatestPublishedAsync(7, Arg.Any<CancellationToken>()).Returns(new List<NewsArticle>());

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        await _newsRepo.Received(1).GetLatestPublishedAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        cts.Cancel();
        _newsRepo.GetLatestPublishedAsync(Arg.Any<int>(), token).Returns(new List<NewsArticle>());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.Handle(new GetLatestNewsQuery(), token));
    }
}