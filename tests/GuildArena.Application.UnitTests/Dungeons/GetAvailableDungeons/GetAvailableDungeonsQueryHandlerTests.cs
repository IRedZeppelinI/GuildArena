using GuildArena.Application.Dungeons.GetAvailableDungeons;
using GuildArena.Domain.Abstractions.Repositories;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Dungeons.GetAvailableDungeons;

public class GetAvailableDungeonsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllDungeons()
    {
        // Arrange
        var repo = Substitute.For<IDungeonDefinitionRepository>();
        var defs = new Dictionary<string, Domain.Definitions.DungeonDefinition>
        {
            ["D1"] = new Domain.Definitions.DungeonDefinition { Id = "D1", Name = "Dungeon One", Description = "desc1", RequiredGuildLevel = 2 },
            ["D2"] = new Domain.Definitions.DungeonDefinition { Id = "D2", Name = "Dungeon Two", Description = null, RequiredGuildLevel = 5 }
        };
        repo.GetAllDefinitions().Returns(defs);

        var handler = new GetAvailableDungeonsQueryHandler(repo);

        // Act
        var result = await handler.Handle(new GetAvailableDungeonsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Id.ShouldBe("D1");
        result.Value[0].Name.ShouldBe("Dungeon One");
        result.Value[0].Description.ShouldBe("desc1");
        result.Value[0].RequiredGuildLevel.ShouldBe(2);
        result.Value[1].Id.ShouldBe("D2");
        result.Value[1].Description.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmpty()
    {
        var repo = Substitute.For<IDungeonDefinitionRepository>();
        repo.GetAllDefinitions().Returns(new Dictionary<string, Domain.Definitions.DungeonDefinition>());
        var handler = new GetAvailableDungeonsQueryHandler(repo);

        var result = await handler.Handle(new GetAvailableDungeonsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }
}