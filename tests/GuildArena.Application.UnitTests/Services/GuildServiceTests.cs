using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Services;

public class GuildServiceTests
{
    private readonly IGuildRepository _guildRepoMock;
    private readonly ICharacterDefinitionRepository _characterRepoMock;
    private readonly ILogger<GuildService> _loggerMock;
    private readonly GuildService _service;

    public GuildServiceTests()
    {
        _guildRepoMock = Substitute.For<IGuildRepository>();
        _characterRepoMock = Substitute.For<ICharacterDefinitionRepository>();
        _loggerMock = Substitute.For<ILogger<GuildService>>();

        // Instanciação limpa
        _service = new GuildService(_guildRepoMock, _characterRepoMock, _loggerMock);
    }

    // --- TESTES: GetGuildRosterAsync ---

    [Fact]
    public async Task GetGuildRosterAsync_WhenGuildIdIsNull_ShouldReturnNotFoundResult()
    {
        // ACT
        var result = await _service.GetGuildRosterAsync(null);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Guild.NotFound");
    }

    [Fact]
    public async Task GetGuildRosterAsync_ShouldReturnMappedHeroes_WithNamesFromJson()
    {
        // ARRANGE
        var guildId = 15;

        // Dados vindos da BD (PostgreSQL)
        var dbHeroes = new List<Hero>
        {
            new Hero { Id = 1, CharacterDefinitionId = "HERO_1", CurrentLevel = 5, GuildId = guildId },
            new Hero { Id = 2, CharacterDefinitionId = "HERO_UNKNOWN", CurrentLevel = 1, GuildId = guildId }
        };
        _guildRepoMock.GetAllHeroesAsync(guildId).Returns(dbHeroes);

        // Dados vindos do JSON (Memória)
        var charDef = new CharacterDefinition
        {
            Id = "HERO_1",
            Name = "Garret",
            RaceId = "RACE_HUMAN",
            BaseStats = new(),
            StatsGrowthPerLevel = new()
        };

        _characterRepoMock.GetAllDefinitions().Returns(new Dictionary<string, CharacterDefinition>
        {
            { "HERO_1", charDef }
        });

        // ACT
        var result = await _service.GetGuildRosterAsync(guildId);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);

        var garret = result.Value.First(h => h.Id == 1);
        garret.Name.ShouldBe("Garret"); // Mapeou com sucesso do JSON
        garret.CurrentLevel.ShouldBe(5);

        var unknown = result.Value.First(h => h.Id == 2);
        unknown.Name.ShouldBe("Unknown Hero"); // Fallback caso o JSON não exista
    }

    // --- TESTES: CreateGuildAsync ---

    [Fact]
    public async Task CreateGuildAsync_WhenUserAlreadyHasGuild_ShouldReturnConflictResult()
    {
        // ACT
        var result = await _service.CreateGuildAsync("user-123", existingGuildId: 10, "My Guild");

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("Guild.AlreadyExists");

        await _guildRepoMock.DidNotReceiveWithAnyArgs().CreateWithStarterPackAsync(default!, default!);
    }
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("AB")]
    [InlineData(null)]
    public async Task CreateGuildAsync_WhenNameIsInvalid_ShouldReturnValidationFailure(string? invalidName)
    {
        // ACT
        var result = await _service.CreateGuildAsync("user-123", null, invalidName!);

        // ASSERT
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Guild.InvalidName");

        await _guildRepoMock.DidNotReceiveWithAnyArgs().CreateWithStarterPackAsync(default!, default!);
    }

    [Fact]
    public async Task CreateGuildAsync_WhenValid_ShouldCreateAndReturnSuccess()
    {
        // ARRANGE
        var userId = "user-123";
        var guildName = "Knights of Cydonia";

        // ACT
        var result = await _service.CreateGuildAsync(userId, null, guildName);

        // ASSERT
        result.IsSuccess.ShouldBeTrue();

        await _guildRepoMock.Received(1).CreateWithStarterPackAsync(userId, guildName);
    }
}