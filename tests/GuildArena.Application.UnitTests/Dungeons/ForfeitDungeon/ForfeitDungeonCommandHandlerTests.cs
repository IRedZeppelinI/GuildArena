using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Dungeons.ForfeitDungeon;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Dungeons.ForfeitDungeon;

public class ForfeitDungeonCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly IDungeonRunRepository _dungeonRunRepo = Substitute.For<IDungeonRunRepository>();
    private readonly ILogger<ForfeitDungeonCommandHandler> _logger = Substitute.For<ILogger<ForfeitDungeonCommandHandler>>();

    private readonly ForfeitDungeonCommandHandler _handler;

    public ForfeitDungeonCommandHandlerTests()
    {
        _handler = new ForfeitDungeonCommandHandler(_currentUser, _guildRepo, _dungeonRunRepo, _logger);
    }

    [Fact]
    public async Task Handle_Unauthorized_ReturnsFailure()
    {
        _currentUser.UserId.Returns((string?)null);
        var result = await _handler.Handle(new ForfeitDungeonCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NoGuild_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns((Guild?)null);
        var result = await _handler.Handle(new ForfeitDungeonCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NoActiveRun_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(new Guild { Id = 1, ApplicationUserId = "user1", Name = "G" });
        _dungeonRunRepo.GetActiveRunAsync(1, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);
        var result = await _handler.Handle(new ForfeitDungeonCommand(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_DeletesRun_ReturnsSuccess()
    {
        _currentUser.UserId.Returns("user1");
        var guild = new Guild { Id = 42, ApplicationUserId = "user1", Name = "G" };
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        var run = new ActiveDungeonRun { Id = 10 };
        _dungeonRunRepo.GetActiveRunAsync(42, Arg.Any<CancellationToken>()).Returns(run);

        var result = await _handler.Handle(new ForfeitDungeonCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _dungeonRunRepo.Received(1).DeleteRunAsync(run, Arg.Any<CancellationToken>());
    }
}