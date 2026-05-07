using GuildArena.Application.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Application.UnitTests.Services;

public class EncounterServiceTests
{
    private readonly IEncounterDefinitionRepository _encounterRepoMock;
    private readonly EncounterService _service;

    public EncounterServiceTests()
    {
        _encounterRepoMock = Substitute.For<IEncounterDefinitionRepository>();
        _service = new EncounterService(_encounterRepoMock);
    }

    [Fact]
    public void GetAvailableEncounters_ShouldReturnMappedDtos_WhenDefinitionsExist()
    {
        // ARRANGE
        var definition1 = new EncounterDefinition
        {
            Id = "ENC_1",
            Name = "Ambush",
            DifficultyRating = 1
        };
        var definition2 = new EncounterDefinition
        {
            Id = "ENC_2",
            Name = "Boss",
            DifficultyRating = 5
        };

        var dict = new Dictionary<string, EncounterDefinition>
        {
            { "ENC_1", definition1 },
            { "ENC_2", definition2 }
        };

        _encounterRepoMock.GetAllDefinitions().Returns(dict);

        // ACT
        var result = _service.GetAvailableEncounters();

        // ASSERT
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Count.ShouldBe(2);

        var first = result.Value.First(e => e.Id == "ENC_1");
        first.Name.ShouldBe("Ambush");
        first.DifficultyRating.ShouldBe(1);
    }

    [Fact]
    public void GetAvailableEncounters_ShouldReturnEmptyList_WhenNoDefinitionsExist()
    {
        // ARRANGE
        _encounterRepoMock.GetAllDefinitions().Returns(new Dictionary<string, EncounterDefinition>());

        // ACT
        var result = _service.GetAvailableEncounters();

        // ASSERT
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }
}