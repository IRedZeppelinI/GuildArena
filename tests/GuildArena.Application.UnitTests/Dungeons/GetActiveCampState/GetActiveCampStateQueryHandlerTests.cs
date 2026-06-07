using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Dungeons.GetActiveCampState;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Dungeons.GetActiveCampState;

public class GetActiveCampStateQueryHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly IDungeonRunRepository _dungeonRunRepo = Substitute.For<IDungeonRunRepository>();
    private readonly IDungeonDefinitionRepository _dungeonDefRepo = Substitute.For<IDungeonDefinitionRepository>();
    private readonly ICharacterDefinitionRepository _charDefRepo = Substitute.For<ICharacterDefinitionRepository>();
    private readonly IRaceDefinitionRepository _raceDefRepo = Substitute.For<IRaceDefinitionRepository>();
    private readonly ILogger<GetActiveCampStateQueryHandler> _logger = Substitute.For<ILogger<GetActiveCampStateQueryHandler>>();

    private readonly GetActiveCampStateQueryHandler _handler;

    public GetActiveCampStateQueryHandlerTests()
    {
        _handler = new GetActiveCampStateQueryHandler(
            _currentUser, _guildRepo, _dungeonRunRepo, _dungeonDefRepo, _charDefRepo, _raceDefRepo, _logger);
    }

    [Fact]
    public async Task Handle_NoAuth_ReturnsUnauthorized()
    {
        _currentUser.UserId.Returns((string?)null);
        var result = await _handler.Handle(new GetActiveCampStateQuery(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_NoGuild_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns((Guild?)null);
        var result = await _handler.Handle(new GetActiveCampStateQuery(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NoActiveRun_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(new Guild { Id = 1, ApplicationUserId = "user1", Name = "G" });
        _dungeonRunRepo.GetActiveRunAsync(1, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);
        var result = await _handler.Handle(new GetActiveCampStateQuery(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_Success_ReturnsCampDto()
    {
        // Arrange
        _currentUser.UserId.Returns("user1");
        var guild = new Guild { Id = 42, ApplicationUserId = "user1", Name = "MyGuild" };
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);

        var dungeonDef = new Domain.Definitions.DungeonDefinition
        {
            Id = "D1",
            Name = "The Pit",
            Stages = new List<Domain.Definitions.DungeonDefinition.DungeonStage>
            {
                new Domain.Definitions.DungeonDefinition.DungeonStage { StageIndex = 0, IsBossNode = false },
                new Domain.Definitions.DungeonDefinition.DungeonStage { StageIndex = 1, IsBossNode = true }
            }
        };
        dungeonDef.Stages[0].StageIndex = 0;
        dungeonDef.Stages[1].StageIndex = 1;
        _dungeonDefRepo.TryGetDefinition("D1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!)
            .Returns(x => { x[1] = dungeonDef; return true; });

        var run = new ActiveDungeonRun
        {
            Id = 10,
            GuildId = 42,
            DungeonDefinitionId = "D1",
            CurrentStageIndex = 1,
            HeroesState = new List<DungeonHeroState>
            {
                new DungeonHeroState { HeroId = 101, CurrentHP = 45 },
                new DungeonHeroState { HeroId = 102, CurrentHP = 70 }
            }
        };
        _dungeonRunRepo.GetActiveRunAsync(42, Arg.Any<CancellationToken>()).Returns(run);

        var heroes = new List<Hero>
        {
            new Hero { Id = 101, CharacterDefinitionId = "HERO_A", CurrentLevel = 1 },
            new Hero { Id = 102, CharacterDefinitionId = "HERO_B", CurrentLevel = 2 }
        };
        _guildRepo.GetAllHeroesAsync(42).Returns(heroes);

        var charDefA = new Domain.Definitions.CharacterDefinition
        {
            Id = "HERO_A",
            Name = "Alaric",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { MaxHP = 100 },
            StatsGrowthPerLevel = new BaseStats()
        };
        var charDefB = new Domain.Definitions.CharacterDefinition
        {
            Id = "HERO_B",
            Name = "Borin",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { MaxHP = 120 },
            StatsGrowthPerLevel = new BaseStats { MaxHP = 10 }
        };
        _charDefRepo.TryGetDefinition("HERO_A", out Arg.Any<Domain.Definitions.CharacterDefinition>()!)
            .Returns(x => { x[1] = charDefA; return true; });
        _charDefRepo.TryGetDefinition("HERO_B", out Arg.Any<Domain.Definitions.CharacterDefinition>()!)
            .Returns(x => { x[1] = charDefB; return true; });
        var raceDef = new Domain.Definitions.RaceDefinition { Id = "RACE_HUMAN", Name = "Human", BonusStats = new BaseStats() };
        _raceDefRepo.TryGetDefinition("RACE_HUMAN", out Arg.Any<Domain.Definitions.RaceDefinition>()!)
            .Returns(x => { x[1] = raceDef; return true; });

        // Act
        var result = await _handler.Handle(new GetActiveCampStateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value;
        dto.DungeonId.ShouldBe("D1");
        dto.DungeonName.ShouldBe("The Pit");
        dto.CurrentStageIndex.ShouldBe(1);
        dto.IsBossNode.ShouldBeTrue();
        dto.Heroes.Count.ShouldBe(2);
        dto.Heroes[0].HeroId.ShouldBe(101);
        dto.Heroes[0].Name.ShouldBe("Alaric");
        dto.Heroes[0].CurrentHP.ShouldBe(45);
        dto.Heroes[0].MaxHP.ShouldBe(100);
        dto.Heroes[1].HeroId.ShouldBe(102);
        dto.Heroes[1].Name.ShouldBe("Borin");
        dto.Heroes[1].CurrentHP.ShouldBe(70);
        dto.Heroes[1].MaxHP.ShouldBe(130); // 120 + 10*(2-1)
    }
}