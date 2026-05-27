using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Tavern.GetTavernShop;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.ValueObjects.UnlockHero;
using GuildArena.Shared.DTOs.Shop;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Tavern.GetTavernShop;

public class GetTavernShopQueryHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly ICharacterDefinitionRepository _charRepo = Substitute.For<ICharacterDefinitionRepository>();
    private readonly IRaceDefinitionRepository _raceRepo = Substitute.For<IRaceDefinitionRepository>();
    private readonly IHeroUnlockEvaluator _evaluator = Substitute.For<IHeroUnlockEvaluator>();
    private readonly ILogger<GetTavernShopQueryHandler> _logger = Substitute.For<ILogger<GetTavernShopQueryHandler>>();

    private readonly GetTavernShopQueryHandler _handler;

    public GetTavernShopQueryHandlerTests()
    {
        _handler = new GetTavernShopQueryHandler(_currentUser, _guildRepo, _charRepo, _raceRepo, _evaluator, _logger);
    }

    [Fact]
    public async Task Handle_NoGuildId_ReturnsFailure()
    {
        _currentUser.GuildId.Returns((int?)null);
        var result = await _handler.Handle(new GetTavernShopQuery(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tavern.NoGuild");
    }

    [Fact]
    public async Task Handle_GuildNotFound_ReturnsFailure()
    {
        _currentUser.GuildId.Returns(1);
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildWithHistoryAsync("user1").Returns((Guild?)null);

        var result = await _handler.Handle(new GetTavernShopQuery(), CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tavern.GuildNotFound");
    }

    [Fact]
    public async Task Handle_ReturnsShopWithOwnedAndLockedHeroes()
    {
        // Arrange
        _currentUser.GuildId.Returns(1);
        _currentUser.UserId.Returns("user1");

        var guild = new Guild { Id = 1, ApplicationUserId = "user1", Name = "G", Level = 1, Gold = 1000 };
        _guildRepo.GetGuildWithHistoryAsync("user1").Returns(guild);

        var garretDef = CreateHeroDef("HERO_GARRET", "RACE_HUMAN", null);
        var vexDef = CreateHeroDef("HERO_VEX", "RACE_KYMERA", new HeroUnlockRequirements
        {
            GoldCost = 1200,
            Conditions = new List<UnlockHeroCondition> { new() { Type = UnlockConditionType.GuildLevel, MinLevel = 2 } }
        });
        _charRepo.GetAllDefinitions().Returns(new Dictionary<string, CharacterDefinition>
        {
            ["HERO_GARRET"] = garretDef,
            ["HERO_VEX"] = vexDef
        });

        _raceRepo.TryGetDefinition("RACE_HUMAN", out Arg.Any<RaceDefinition>()).Returns(x =>
        {
            x[1] = new RaceDefinition { Id = "RACE_HUMAN", Name = "Human" };
            return true;
        });
        _raceRepo.TryGetDefinition("RACE_KYMERA", out Arg.Any<RaceDefinition>()).Returns(x =>
        {
            x[1] = new RaceDefinition { Id = "RACE_KYMERA", Name = "Kymera" };
            return true;
        });

        var ownedHeroes = new List<Hero>
        {
            new() { Id = 10, GuildId = 1, CharacterDefinitionId = "HERO_GARRET", CurrentLevel = 1 }
        };
        _guildRepo.GetAllHeroesAsync(1).Returns(ownedHeroes);

        // Vex is locked
        _evaluator.AreConditionsMet(guild, vexDef.UnlockRequirements!).Returns(false);
        _evaluator.GetProgress(guild, vexDef.UnlockRequirements!.Conditions)
            .Returns(new List<UnlockConditionDto>
            {
                new() { Type = UnlockConditionType.GuildLevel, RequiredValue = 2, CurrentValue = 1, Description = "Guild Lv 2" }
            });

        // Act
        var result = await _handler.Handle(new GetTavernShopQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var shop = result.Value;
        shop.GuildGold.ShouldBe(1000);
        shop.Heroes.Count.ShouldBe(2);

        var garret = shop.Heroes.First(h => h.DefinitionId == "HERO_GARRET");
        garret.Id.ShouldBe(10);
        garret.Status.ShouldBe(HeroStatus.Owned);
        garret.GoldCost.ShouldBe(0);

        var vex = shop.Heroes.First(h => h.DefinitionId == "HERO_VEX");
        vex.Id.ShouldBeNull();
        vex.Status.ShouldBe(HeroStatus.Locked);
        vex.GoldCost.ShouldBe(1200);
        vex.UnlockConditions.Count.ShouldBe(1);
        vex.UnlockConditions[0].Type.ShouldBe(UnlockConditionType.GuildLevel);
        vex.UnlockConditions[0].CurrentValue.ShouldBe(1);
    }

    private CharacterDefinition CreateHeroDef(string id, string raceId, HeroUnlockRequirements? unlock) => new()
    {
        Id = id,
        Name = id,
        RaceId = raceId,
        BaseStats = new(),
        StatsGrowthPerLevel = new(),
        UnlockRequirements = unlock
    };
}