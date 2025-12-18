using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class ManipulateEssenceHandlerTests
{
    private readonly IEssenceService _essenceServiceMock;
    private readonly ILogger<ManipulateEssenceHandler> _loggerMock;
    private readonly ManipulateEssenceHandler _handler;
    private readonly GameState _gameState;
    private readonly IBattleLogService _battleLogService;

    public ManipulateEssenceHandlerTests()
    {
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _loggerMock = Substitute.For<ILogger<ManipulateEssenceHandler>>();
        _battleLogService = Substitute.For<IBattleLogService>();
        _handler = new ManipulateEssenceHandler(_essenceServiceMock, _loggerMock, _battleLogService);

        var p1 = new CombatPlayer { PlayerId = 1 };
        var p2 = new CombatPlayer { PlayerId = 2 };
        _gameState = new GameState { Players = new List<CombatPlayer> { p1, p2 } };
    }

    [Fact]
    public void Apply_Channeling_ShouldGiveEssenceToCaster_AndLog()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self",
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Mind, Amount = 1 }
            }
        };

        var caster = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Mage",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, caster, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 1),
            EssenceType.Mind,
            1
        );
        _battleLogService.
            Received(1).Log(Arg.Is<string>(s => s.Contains("gained 1 Mind Essence")));
    }

    [Fact]
    public void Apply_GiftToAlly_ShouldGiveEssenceToAllyOwner_AndLog()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Ally",
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Vigor, Amount = 2 }
            }
        };

        var caster = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Support",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var allyTarget = new Combatant
        {
            Id = 11,
            OwnerId = 1,
            Name = "Warrior",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, allyTarget, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 1),
            EssenceType.Vigor,
            2
        );
        _battleLogService
            .Received(1)
            .Log(Arg.Is<string>(s => s.Contains("Warrior gained 2 Vigor Essence")));
    }

    [Fact]
    public void Apply_CursedGift_ShouldGiveEssenceToEnemy()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Enemy",
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Shadow, Amount = 1 }
            }
        };

        var caster = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Warlock",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };

        var enemyTarget = new Combatant
        {
            Id = 20,
            OwnerId = 2,
            Name = "Paladin",
            RaceId = "RACE_B",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, enemyTarget, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 2),
            EssenceType.Shadow,
            1
        );
        _battleLogService
            .Received(1)
            .Log(Arg.Is<string>(s => s.Contains("Paladin gained 1 Shadow Essence")));
    }

    [Fact]
    public void Apply_MultipleEssences_ShouldCallAddEssenceMultipleTimes()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self",
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Mind, Amount = 1 },
                new EssenceAmount { Type = EssenceType.Neutral, Amount = 1 }
            }
        };

        var caster = new Combatant
        {
            Id = 10,
            OwnerId = 1,
            Name = "Psylian",
            RaceId = "RACE_PSYLIAN",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, caster, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.
            Received(1).AddEssence(Arg.Any<CombatPlayer>(), EssenceType.Mind, 1);
        _essenceServiceMock.
            Received(1).AddEssence(Arg.Any<CombatPlayer>(), EssenceType.Neutral, 1);
        _battleLogService.Received(2).Log(Arg.Any<string>());
    }

    [Fact]
    public void Apply_EmptyDefinition_ShouldLogWarningAndDoNothing()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self",
            EssenceManipulations = new List<EssenceAmount>()
        };
        var caster = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Caster",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, caster, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.DidNotReceive().AddEssence(
            Arg.Any<CombatPlayer>(),
            Arg.Any<EssenceType>(),
            Arg.Any<int>());

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("list is empty")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        _battleLogService.Received(0).Log(Arg.Any<string>());
    }

    [Fact]
    public void Apply_TargetWithoutPlayer_ShouldLogWarning()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Mob",
            EssenceManipulations = new List<EssenceAmount> { new EssenceAmount { Amount = 1 } }
        };

        var caster = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Caster",
            RaceId = "RACE_A",
            CurrentHP = 100,
            BaseStats = new()
        };
        var neutralMob = new Combatant
        {
            Id = 99,
            OwnerId = 0,
            Name = "Neutral Creep",
            RaceId = "RACE_NEUTRAL",
            CurrentHP = 100,
            BaseStats = new()
        };
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effect, caster, neutralMob, _gameState, actionResult);

        // ASSERT
        _essenceServiceMock.DidNotReceive().AddEssence(
            Arg.Any<CombatPlayer>(),
            Arg.Any<EssenceType>(),
            Arg.Any<int>());

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("has no valid player")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}