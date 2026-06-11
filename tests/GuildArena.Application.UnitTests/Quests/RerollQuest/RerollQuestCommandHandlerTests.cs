using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Quests.RerollQuest;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Quests.RerollQuest;

public class RerollQuestCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IQuestService _questService;
    private readonly ILogger<RerollQuestCommandHandler> _logger;
    private readonly RerollQuestCommandHandler _sut; // System Under Test

    public RerollQuestCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentUserService>();
        _guildRepo = Substitute.For<IGuildRepository>();
        _questService = Substitute.For<IQuestService>();
        _logger = Substitute.For<ILogger<RerollQuestCommandHandler>>();

        _sut = new RerollQuestCommandHandler(_currentUser, _guildRepo, _questService, _logger);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);
        var command = new RerollQuestCommand { QuestId = 1 };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenGuildNotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns("user_123");
        _guildRepo.GetGuildWithQuestsAsync("user_123").Returns((Guild?)null);
        var command = new RerollQuestCommand { QuestId = 1 };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Guild.NotFound");
    }

    [Fact]
    public async Task Handle_WhenQuestServiceFails_PropagatesFailureAndDoesNotSave()
    {
        // Arrange
        string userId = "user_123";
        _currentUser.UserId.Returns(userId);

        var guild = new Guild { Id = 1, ApplicationUserId = userId, Name = "Test" };
        _guildRepo.GetGuildWithQuestsAsync(userId).Returns(guild);

        // Simulamos o serviço a devolver um erro (ex: O jogador já usou o Reroll de hoje)
        var expectedError = new Error("Quests.RerollUsed", "Used", ErrorType.Conflict);
        _questService.RerollQuestAsync(guild, 10).Returns(Result.Failure(expectedError));

        var command = new RerollQuestCommand { QuestId = 10 };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedError);

        // Garante que se houve erro, NÃO gravamos dados inválidos na Base de Dados
        await _guildRepo.DidNotReceive().UpdateGuildAsync(Arg.Any<Guild>());
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SavesAndReturnsSuccess()
    {
        // Arrange
        string userId = "user_123";
        _currentUser.UserId.Returns(userId);

        var guild = new Guild { Id = 1, ApplicationUserId = userId, Name = "Test" };
        _guildRepo.GetGuildWithQuestsAsync(userId).Returns(guild);

        _questService.RerollQuestAsync(guild, 10).Returns(Result.Success());

        var command = new RerollQuestCommand { QuestId = 10 };

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Garante que o Update foi chamado
        await _guildRepo.Received(1).UpdateGuildAsync(guild);
    }
}