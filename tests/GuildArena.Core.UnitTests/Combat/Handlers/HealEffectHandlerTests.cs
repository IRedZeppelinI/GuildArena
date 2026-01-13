using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Modifiers;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.ValueObjects.Stats;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class HealEffectHandlerTests
{
    private readonly IStatCalculationService _statServiceMock;
    private readonly ITriggerProcessor _triggerProcessorMock;
    private readonly IBattleLogService _battleLogMock;
    private readonly ILogger<HealEffectHandler> _loggerMock;
    private readonly HealEffectHandler _handler;

    public HealEffectHandlerTests()
    {
        _statServiceMock = Substitute.For<IStatCalculationService>();
        _triggerProcessorMock = Substitute.For<ITriggerProcessor>();
        _battleLogMock = Substitute.For<IBattleLogService>();
        _loggerMock = Substitute.For<ILogger<HealEffectHandler>>(); 

        _handler = new HealEffectHandler(
            _statServiceMock,
            _triggerProcessorMock,
            _battleLogMock,
            _loggerMock);
    }

    [Fact]
    public void Apply_ShouldHealTarget_BasedOnStats()
    {
        // ARRANGE
        var source = new Combatant
        {
            Id = 1,
            Name = "Healer",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Tank",
            RaceId = "RACE_TEST",
            CurrentHP = 50,
            MaxHP = 100,
            BaseStats = new()
        };

        var effect = new EffectDefinition
        {
            TargetRuleId = "TGT_ALLY", // Required
            ScalingStat = StatType.Magic,
            ScalingFactor = 2.0f,
            BaseAmount = 10
        };

        // Source tem 10 Magic. Formula: 10 + (10 * 2.0) = 30 Cura.
        _statServiceMock.GetStatValue(source, StatType.Magic).Returns(10f);

        var result = new CombatActionResult();

        // ACT
        _handler.Apply(effect, source, target, new GameState(), result);

        // ASSERT
        target.CurrentHP.ShouldBe(80); // 50 + 30
        _battleLogMock.Received().Log(Arg.Is<string>(s => s.Contains("healed Tank for 30 HP")));
        result.ResultTags.ShouldContain("Heal");
    }

    [Fact]
    public void Apply_ShouldCapAtMaxHP()
    {
        // ARRANGE
        var source = new Combatant
        {
            Id = 1,
            Name = "Healer",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new()
        };
        // Target tem 90/100 HP. Só precisa de 10.
        var target = new Combatant
        {
            Id = 2,
            Name = "Tank",
            RaceId = "RACE_TEST",
            CurrentHP = 90,
            MaxHP = 100,
            BaseStats = new()
        };

        var effect = new EffectDefinition
        {
            TargetRuleId = "TGT_ALLY",
            ScalingStat = StatType.Magic,
            ScalingFactor = 0,
            BaseAmount = 50 // Cura excessiva (50)
        };

        _statServiceMock.GetStatValue(source, StatType.Magic).Returns(0);

        // ACT
        _handler.Apply(effect, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(100); // Não deve passar de 100

        // Log deve reportar o que realmente curou (10), não o potencial (50)
        _battleLogMock.Received().Log(Arg.Is<string>(s => s.Contains("healed Tank for 10 HP")));
    }

    [Fact]
    public void Apply_ShouldFireTriggers_WhenHealOccurs()
    {
        // ARRANGE
        var source = new Combatant
        {
            Id = 1,
            Name = "Healer",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Tank",
            RaceId = "RACE_TEST",
            CurrentHP = 10,
            MaxHP = 100,
            BaseStats = new()
        };

        var effect = new EffectDefinition { TargetRuleId = "TGT_ALLY", BaseAmount = 10 };

        // ACT
        _handler.Apply(effect, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        // Verifica trigger ofensivo (Healer curou)
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_DEAL_HEAL,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target && c.Value == 10));

        // Verifica trigger defensivo (Tank recebeu cura)
        _triggerProcessorMock.Received(1).ProcessTriggers(
            ModifierTrigger.ON_RECEIVE_HEAL,
            Arg.Is<TriggerContext>(c => c.Source == source && c.Target == target));
    }

    [Fact]
    public void Apply_WhenTargetFullHP_ShouldNotFireTriggers()
    {
        // ARRANGE
        var source = new Combatant
        {
            Id = 1,
            Name = "Healer",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Tank",
            RaceId = "RACE_TEST",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new()
        };

        var effect = new EffectDefinition { TargetRuleId = "TGT_ALLY", BaseAmount = 50 };

        // ACT
        _handler.Apply(effect, source, target, new GameState(), new CombatActionResult());

        // ASSERT
        target.CurrentHP.ShouldBe(100);

        // Log específico de "Already Full"
        _battleLogMock.Received().Log(Arg.Is<string>(s => s.Contains("Already Full")));

        // Não deve disparar triggers se a cura efetiva foi 0
        _triggerProcessorMock.DidNotReceiveWithAnyArgs().ProcessTriggers(default, default!);
    }
}