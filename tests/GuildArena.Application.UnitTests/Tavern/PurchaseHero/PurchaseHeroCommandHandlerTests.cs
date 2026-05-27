using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Application.Tavern.PurchaseHero;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.UnlockHero;
using GuildArena.Shared.DTOs.Shop;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Tavern.PurchaseHero;

public class PurchaseHeroCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IGuildRepository _guildRepo = Substitute.For<IGuildRepository>();
    private readonly ICharacterDefinitionRepository _charRepo = Substitute.For<ICharacterDefinitionRepository>();
    private readonly IHeroUnlockEvaluator _evaluator = Substitute.For<IHeroUnlockEvaluator>();
    private readonly IHeroPurchaseRepository _purchaseRepo = Substitute.For<IHeroPurchaseRepository>();
    private readonly ILogger<PurchaseHeroCommandHandler> _logger = Substitute.For<ILogger<PurchaseHeroCommandHandler>>();

    private readonly PurchaseHeroCommandHandler _handler;

    public PurchaseHeroCommandHandlerTests()
    {
        _handler = new PurchaseHeroCommandHandler(_currentUser, _guildRepo, _charRepo, _evaluator, _purchaseRepo, _logger);
    }

    [Fact]
    public async Task Handle_NoGuild_ReturnsFailure()
    {
        _currentUser.GuildId.Returns((int?)null);
        var cmd = new PurchaseHeroCommand { HeroId = "HERO_VEX" };
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.NoGuild");
    }

    [Fact]
    public async Task Handle_GuildNotFound_ReturnsFailure()
    {
        _currentUser.GuildId.Returns(1);
        _currentUser.UserId.Returns("user1");
        _guildRepo.GetGuildWithHistoryAsync("user1").Returns((Guild?)null);

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.GuildNotFound");
    }

    [Fact]
    public async Task Handle_HeroNotFound_ReturnsFailure()
    {
        SetupUserAndGuild(1, "user1", out var guild);
        _charRepo.TryGetDefinition("HERO_VEX", out Arg.Any<CharacterDefinition>()).Returns(false);

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.HeroNotFound");
    }

    [Fact]
    public async Task Handle_StarterHero_ReturnsFailure()
    {
        SetupUserAndGuild(1, "user1", out var guild);
        var def = new CharacterDefinition
        {
            Id = "HERO_GARRET",
            Name = "Garret",
            RaceId = "RACE_HUMAN",
            BaseStats = new(),
            StatsGrowthPerLevel = new(),
            UnlockRequirements = null
        };
        _charRepo.TryGetDefinition("HERO_GARRET", out Arg.Any<CharacterDefinition>()).Returns(x => { x[1] = def; return true; });

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_GARRET" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.StarterHero");
    }

    [Fact]
    public async Task Handle_AlreadyOwned_ReturnsFailure()
    {
        SetupUserAndGuild(1, "user1", out var guild);
        var def = CreateDefWithUnlock("HERO_VEX", 1200);
        _charRepo.TryGetDefinition("HERO_VEX", out Arg.Any<CharacterDefinition>()).Returns(x => { x[1] = def; return true; });

        guild.Heroes = new List<Hero> { new() { GuildId = 1, CharacterDefinitionId = "HERO_VEX" } };
        _guildRepo.GetAllHeroesAsync(1).Returns(guild.Heroes.ToList());

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.AlreadyOwned");
    }

    [Fact]
    public async Task Handle_ConditionsNotMet_ReturnsFailure()
    {
        SetupUserAndGuild(1, "user1", out var guild);
        var def = CreateDefWithUnlock("HERO_VEX", 1200);
        _charRepo.TryGetDefinition("HERO_VEX", out Arg.Any<CharacterDefinition>()).Returns(x => { x[1] = def; return true; });
        _guildRepo.GetAllHeroesAsync(1).Returns(new List<Hero>());
        _evaluator.AreConditionsMet(guild, def.UnlockRequirements!).Returns(false);

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.ConditionsNotMet");
    }

    [Fact]
    public async Task Handle_InsufficientGold_ReturnsFailure()
    {
        SetupUserAndGuild(1, "user1", out var guild);
        guild.Gold = 500;
        var def = CreateDefWithUnlock("HERO_VEX", 1200);
        _charRepo.TryGetDefinition("HERO_VEX", out Arg.Any<CharacterDefinition>()).Returns(x => { x[1] = def; return true; });
        _guildRepo.GetAllHeroesAsync(1).Returns(new List<Hero>());
        _evaluator.AreConditionsMet(guild, def.UnlockRequirements!).Returns(true);

        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Purchase.InsufficientGold");
    }

    [Fact]
    public async Task Handle_Success_UpdatesGoldAddsHeroRecordsPurchase()
    {
        // Arrange
        SetupUserAndGuild(1, "user1", out var guild);
        guild.Gold = 2000;
        var unlock = new HeroUnlockRequirements { GoldCost = 1200, Conditions = new List<UnlockHeroCondition>() };
        var def = new CharacterDefinition
        {
            Id = "HERO_VEX",
            Name = "Vex",
            RaceId = "RACE_KYMERA",
            BaseStats = new(),
            StatsGrowthPerLevel = new(),
            UnlockRequirements = unlock
        };
        _charRepo.TryGetDefinition("HERO_VEX", out Arg.Any<CharacterDefinition>()).Returns(x => { x[1] = def; return true; });
        _guildRepo.GetAllHeroesAsync(1).Returns(new List<Hero>());
        _evaluator.AreConditionsMet(guild, unlock).Returns(true);

        // Act
        var result = await _handler.Handle(new PurchaseHeroCommand { HeroId = "HERO_VEX" }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Success.ShouldBeTrue();
        result.Value.UpdatedGold.ShouldBe(800);
        guild.Gold.ShouldBe(800);

        await _guildRepo.Received(1).UpdateGuildAsync(guild);

        guild.Heroes.ShouldHaveSingleItem();
        guild.Heroes.First().CharacterDefinitionId.ShouldBe("HERO_VEX");

        await _purchaseRepo.Received(1).AddAsync(Arg.Is<HeroPurchase>(p =>
            p.GuildId == 1 && p.CharacterDefinitionId == "HERO_VEX" && p.GoldPaid == 1200
        ), Arg.Any<CancellationToken>());
    }

    private void SetupUserAndGuild(int guildId, string userId, out Guild guild)
    {
        _currentUser.GuildId.Returns(guildId);
        _currentUser.UserId.Returns(userId);
        guild = new Guild { Id = guildId, ApplicationUserId = userId, Name = "G", Level = 1, Gold = 1000 };
        _guildRepo.GetGuildWithHistoryAsync(userId).Returns(guild);
    }

    private CharacterDefinition CreateDefWithUnlock(string id, int goldCost) =>
        new()
        {
            Id = id,
            Name = id,
            RaceId = "RACE_HUMAN",
            BaseStats = new(),
            StatsGrowthPerLevel = new(),
            UnlockRequirements = new HeroUnlockRequirements { GoldCost = goldCost, Conditions = new() }
        };
}