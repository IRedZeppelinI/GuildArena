using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Dungeons.StartDungeon;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Dungeons.StartDungeon;

public class StartDungeonCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly IDungeonRunRepository _dungeonRunRepo = Substitute.For<IDungeonRunRepository>();
    private readonly IDungeonDefinitionRepository _dungeonDefRepo = Substitute.For<IDungeonDefinitionRepository>();
    private readonly ICharacterDefinitionRepository _charDefRepo = Substitute.For<ICharacterDefinitionRepository>();
    private readonly IRaceDefinitionRepository _raceDefRepo = Substitute.For<IRaceDefinitionRepository>();
    private readonly ILogger<StartDungeonCommandHandler> _logger = Substitute.For<ILogger<StartDungeonCommandHandler>>();

    private readonly StartDungeonCommandHandler _handler;

    public StartDungeonCommandHandlerTests()
    {
        _handler = new StartDungeonCommandHandler(
            _currentUser,
            _guildRepo,
            _dungeonRunRepo,
            _dungeonDefRepo,
            _charDefRepo,
            _raceDefRepo,
            _logger);
    }

    [Fact]
    public async Task Handle_UserNotAuthenticated_ReturnsUnauthorized()
    {
        _currentUser.UserId.Returns((string?)null);

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_GuildNotFound_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildByUserIdAsync("user1").Returns((Guild?)null);

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_NotExactlyThreeHeroes_ReturnsValidationError()
    {
        _currentUser.UserId.Returns("user1");
        var guild = CreateGuild();
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        // Return only 2 heroes
        _guildRepo.GetHeroesAsync(guild.Id, Arg.Any<List<int>>()).Returns(new List<Hero> { new Hero { Id = 1, CharacterDefinitionId = "H1" }, new Hero { Id = 2, CharacterDefinitionId = "H2" } });

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_ExistingActiveRun_ReturnsConflict()
    {
        _currentUser.UserId.Returns("user1");
        var guild = CreateGuild();
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        _guildRepo.GetHeroesAsync(guild.Id, Arg.Any<List<int>>()).Returns(CreateThreeHeroes());
        _dungeonRunRepo.GetActiveRunAsync(guild.Id, Arg.Any<CancellationToken>()).Returns(new ActiveDungeonRun { Id = 99 });

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Handle_DungeonDefinitionNotFound_ReturnsNotFound()
    {
        _currentUser.UserId.Returns("user1");
        var guild = CreateGuild();
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        _guildRepo.GetHeroesAsync(guild.Id, Arg.Any<List<int>>()).Returns(CreateThreeHeroes());
        _dungeonRunRepo.GetActiveRunAsync(guild.Id, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);
        _dungeonDefRepo.TryGetDefinition("D1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!).Returns(false);

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_GuildLevelTooLow_ReturnsForbidden()
    {
        _currentUser.UserId.Returns("user1");
        var guild = CreateGuild(); // level 1
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        _guildRepo.GetHeroesAsync(guild.Id, Arg.Any<List<int>>()).Returns(CreateThreeHeroes());
        _dungeonRunRepo.GetActiveRunAsync(guild.Id, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);

        var dungeonDef = new Domain.Definitions.DungeonDefinition
        {
            Id = "D1",
            Name = "Test",
            RequiredGuildLevel = 5, // too high
            Stages = new List<Domain.Definitions.DungeonDefinition.DungeonStage>()
        };
        _dungeonDefRepo.TryGetDefinition("D1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!)
            .Returns(x => { x[1] = dungeonDef; return true; });

        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_Success_CreatesRunWithCorrectHeroHP()
    {
        // Arrange
        _currentUser.UserId.Returns("user1");
        var guild = CreateGuild(level: 5);
        _guildRepo.GetGuildByUserIdAsync("user1").Returns(guild);
        _guildRepo.GetHeroesAsync(guild.Id, Arg.Any<List<int>>()).Returns(CreateThreeHeroes());
        _dungeonRunRepo.GetActiveRunAsync(guild.Id, Arg.Any<CancellationToken>()).Returns((ActiveDungeonRun?)null);

        var dungeonDef = new Domain.Definitions.DungeonDefinition
        {
            Id = "D1",
            Name = "Test Dungeon",
            RequiredGuildLevel = 3,
            Stages = new List<Domain.Definitions.DungeonDefinition.DungeonStage>()
        };
        _dungeonDefRepo.TryGetDefinition("D1", out Arg.Any<Domain.Definitions.DungeonDefinition>()!)
            .Returns(x => { x[1] = dungeonDef; return true; });

        // Mocks for ComputeMaxHp (character & race)
        var charDef = new Domain.Definitions.CharacterDefinition
        {
            Id = "H1",
            Name = "Test Hero",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { MaxHP = 80, Attack = 0, Defense = 0, Agility = 0, Magic = 0, MagicDefense = 0 },
            StatsGrowthPerLevel = new BaseStats { MaxHP = 10, Attack = 0, Defense = 0, Agility = 0, Magic = 0, MagicDefense = 0 }
        };
        var raceDef = new Domain.Definitions.RaceDefinition
        {
            Id = "RACE_HUMAN",
            Name = "Human",
            BonusStats = new BaseStats { MaxHP = 0 }
        };
        _charDefRepo.TryGetDefinition("H1", out Arg.Any<Domain.Definitions.CharacterDefinition>()!)
            .Returns(x => { x[1] = charDef; return true; });
        _raceDefRepo.TryGetDefinition("RACE_HUMAN", out Arg.Any<Domain.Definitions.RaceDefinition>()!)
            .Returns(x => { x[1] = raceDef; return true; });

        // Act
        var command = new StartDungeonCommand { DungeonId = "D1", HeroInstanceIds = new List<int> { 1, 2, 3 } };
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _dungeonRunRepo.Received(1).CreateRunAsync(
            Arg.Is<ActiveDungeonRun>(r =>
                r.GuildId == guild.Id &&
                r.DungeonDefinitionId == "D1" &&
                r.CurrentStageIndex == 0 &&
                r.HeroesState.Count == 3 &&
                r.HeroesState.All(hs => hs.CurrentHP == 80 + (IsLevel2OrHigher(hs.HeroId) ? 10 * (GetLevelOfHero(hs.HeroId) - 1) : 0)) // adjust if needed
            ),
            Arg.Any<CancellationToken>());
    }

    // Helper methods
    private Guild CreateGuild(int level = 1)
    {
        return new Guild { Id = 42, ApplicationUserId = "user1", Name = "TestGuild", Level = level };
    }

    private List<Hero> CreateThreeHeroes()
    {
        return new List<Hero>
        {
            new Hero { Id = 1, CharacterDefinitionId = "H1", CurrentLevel = 1, GuildId = 42 },
            new Hero { Id = 2, CharacterDefinitionId = "H1", CurrentLevel = 2, GuildId = 42 },
            new Hero { Id = 3, CharacterDefinitionId = "H1", CurrentLevel = 3, GuildId = 42 }
        };
    }

    private bool IsLevel2OrHigher(int heroId) => heroId != 1;
    private int GetLevelOfHero(int heroId) => heroId; // 1 -> level1, 2->level2 ...
}