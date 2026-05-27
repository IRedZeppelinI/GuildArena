using GuildArena.Application.Abstractions;
using GuildArena.Application.Services;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.ValueObjects.UnlockHero;
using NSubstitute;
using Shouldly;

namespace GuildArena.Application.UnitTests.Services;

public class HeroUnlockEvaluatorTests
{
    private readonly ICharacterDefinitionRepository _characterRepo = Substitute.For<ICharacterDefinitionRepository>();
    private readonly HeroUnlockEvaluator _evaluator;

    public HeroUnlockEvaluatorTests()
    {
        _evaluator = new HeroUnlockEvaluator(_characterRepo);
    }

    [Fact]
    public void AreConditionsMet_AllConditionsSatisfied_ReturnsTrue()
    {
        // Arrange
        var guild = CreateGuild(level: 3);
        SetupRace("HERO_GARRET", "RACE_HUMAN");
        guild.MatchHistory = new List<MatchParticipant>
        {
            CreateParticipant(isWinner: false, heroDefIds: new[] { "HERO_GARRET" })
        }.ToList();
        // GuildLevel min 3, matches played with RACE_HUMAN min 1
        var requirements = new HeroUnlockRequirements
        {
            GoldCost = 1000,
            Conditions = new List<UnlockHeroCondition>
            {
                new() { Type = UnlockConditionType.GuildLevel, MinLevel = 3, Description = "Lv 3" },
                new() { Type = UnlockConditionType.MatchesPlayedWithRace, RaceId = "RACE_HUMAN", MinCount = 1, Description = "Play 1" }
            }
        };

        // Act
        bool result = _evaluator.AreConditionsMet(guild, requirements);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AreConditionsMet_GuildLevelTooLow_ReturnsFalse()
    {
        var guild = CreateGuild(level: 2);
        var requirements = new HeroUnlockRequirements
        {
            GoldCost = 1000,
            Conditions = new List<UnlockHeroCondition>
            {
                new() { Type = UnlockConditionType.GuildLevel, MinLevel = 3, Description = "Lv 3" }
            }
        };

        bool result = _evaluator.AreConditionsMet(guild, requirements);
        result.ShouldBeFalse();
    }

    [Fact]
    public void AreConditionsMet_NotEnoughMatchesPlayed_ReturnsFalse()
    {
        var guild = CreateGuild(level: 5);
        SetupRace("HERO_GARRET", "RACE_HUMAN");
        SetupRace("HERO_KORG", "RACE_VALDRIN");
        guild.MatchHistory = new List<MatchParticipant>
        {
            CreateParticipant(isWinner: false, heroDefIds: new[] { "HERO_KORG" }) // wrong race
        };

        var requirements = new HeroUnlockRequirements
        {
            GoldCost = 500,
            Conditions = new List<UnlockHeroCondition>
            {
                new() { Type = UnlockConditionType.MatchesPlayedWithRace, RaceId = "RACE_HUMAN", MinCount = 2, Description = "Play 2" }
            }
        };

        bool result = _evaluator.AreConditionsMet(guild, requirements);
        result.ShouldBeFalse();
    }

    [Fact]
    public void AreConditionsMet_NotEnoughMatchesWon_ReturnsFalse()
    {
        var guild = CreateGuild(level: 5);
        SetupRace("HERO_GARRET", "RACE_HUMAN");
        guild.MatchHistory = new List<MatchParticipant>
        {
            CreateParticipant(isWinner: false, heroDefIds: new[] { "HERO_GARRET" }) // lost
        };

        var requirements = new HeroUnlockRequirements
        {
            GoldCost = 500,
            Conditions = new List<UnlockHeroCondition>
            {
                new() { Type = UnlockConditionType.MatchesWonWithRace, RaceId = "RACE_HUMAN", MinCount = 1, Description = "Win 1" }
            }
        };

        bool result = _evaluator.AreConditionsMet(guild, requirements);
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetProgress_ReturnsCorrectValues()
    {
        var guild = CreateGuild(level: 2);
        SetupRace("HERO_VEX", "RACE_KYMERA");
        guild.MatchHistory = new List<MatchParticipant>
        {
            CreateParticipant(isWinner: true, heroDefIds: new[] { "HERO_VEX" })
        };

        var conditions = new List<UnlockHeroCondition>
        {
            new() { Type = UnlockConditionType.GuildLevel, MinLevel = 3, Description = "Guild Lv 3" },
            new() { Type = UnlockConditionType.MatchesWonWithRace, RaceId = "RACE_KYMERA", MinCount = 2, Description = "Win 2 with Kymera" }
        };

        var progress = _evaluator.GetProgress(guild, conditions);

        progress.Count.ShouldBe(2);
        progress[0].Type.ShouldBe(UnlockConditionType.GuildLevel);
        progress[0].RequiredValue.ShouldBe(3);
        progress[0].CurrentValue.ShouldBe(2);
        progress[0].Description.ShouldBe("Guild Lv 3");

        progress[1].Type.ShouldBe(UnlockConditionType.MatchesWonWithRace);
        progress[1].RequiredValue.ShouldBe(2);
        progress[1].CurrentValue.ShouldBe(1);
        progress[1].Description.ShouldBe("Win 2 with Kymera");
    }

    // Helpers
    private Guild CreateGuild(int level) => new()
    {
        Id = 1,
        ApplicationUserId = "user1",
        Name = "TestGuild",
        Level = level,
        Gold = 5000
    };

    private MatchParticipant CreateParticipant(bool isWinner, string[] heroDefIds) => new()
    {
        Id = Guid.NewGuid(),
        IsWinner = isWinner,
        HeroesUsed = heroDefIds.Select(id => new MatchHeroEntry
        {
            HeroDefinitionId = id,
            LevelSnapshot = 1
        }).ToList()
    };

    private void SetupRace(string heroDefId, string raceId)
    {
        var def = new CharacterDefinition
        {
            Id = heroDefId,
            Name = "Test",
            RaceId = raceId,
            BaseStats = new(),
            StatsGrowthPerLevel = new()
        };
        _characterRepo.TryGetDefinition(heroDefId, out Arg.Any<CharacterDefinition>())
            .Returns(x =>
            {
                x[1] = def;
                return true;
            });
    }
}