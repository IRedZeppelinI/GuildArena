using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Quests.GetActiveQuests;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Quests.GetActiveQuests;

public class GetActiveQuestsQueryHandlerTests
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IQuestDefinitionRepository _questDefRepo;
    private readonly IQuestService _questService;
    private readonly ILogger<GetActiveQuestsQueryHandler> _logger;
    private readonly GetActiveQuestsQueryHandler _sut; // System Under Test

    public GetActiveQuestsQueryHandlerTests()
    {
        // Instancia os Mocks
        _currentUser = Substitute.For<ICurrentUserService>();
        _guildRepo = Substitute.For<IGuildRepository>();
        _questDefRepo = Substitute.For<IQuestDefinitionRepository>();
        _questService = Substitute.For<IQuestService>();
        _logger = Substitute.For<ILogger<GetActiveQuestsQueryHandler>>();

        // Cria a classe que estamos a testar
        _sut = new GetActiveQuestsQueryHandler(
            _currentUser, _guildRepo, _questDefRepo, _questService, _logger);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await _sut.Handle(new GetActiveQuestsQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Auth.Unauthorized");
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenGuildNotFound_ReturnsNotFound()
    {
        // Arrange
        _currentUser.UserId.Returns("user_123");
        _guildRepo.GetGuildWithQuestsAsync("user_123").Returns((Guild?)null);

        // Act
        var result = await _sut.Handle(new GetActiveQuestsQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Guild.NotFound");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_GrantsQuestsSavesAndReturnsMappedDtos()
    {
        // Arrange
        string userId = "user_123";
        _currentUser.UserId.Returns(userId);

        var guild = new Guild
        {
            Id = 1,
            ApplicationUserId = userId,
            Name = "Test Guild",
            ActiveQuests = new List<ActiveQuest>
            {
                new ActiveQuest { Id = 10, QuestDefinitionId = "Q1", CurrentProgress = 2, IsCompleted = false }
            }
        };
        _guildRepo.GetGuildWithQuestsAsync(userId).Returns(guild);

        var defs = new Dictionary<string, QuestDefinition>
        {
            { "Q1", new QuestDefinition { Id = "Q1", Name = "Kill Goblins", Description = "...", RewardGold = 50, RewardXP = 100, TargetValue = 5 } }
        };
        _questDefRepo.GetAllDefinitions().Returns(defs);

        // Act
        var result = await _sut.Handle(new GetActiveQuestsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(1);

        var dto = result.Value.First();
        dto.Id.ShouldBe(10);
        dto.Name.ShouldBe("Kill Goblins");
        dto.RewardGold.ShouldBe(50);
        dto.CurrentProgress.ShouldBe(2);
        dto.IsCompleted.ShouldBeFalse();

        // Verifica se o Handler não se esqueceu de chamar os serviços e gravar na Base de Dados
        await _questService.Received(1).GrantDailyQuestsIfNeededAsync(guild);
        await _guildRepo.Received(1).UpdateGuildAsync(guild);
    }
}