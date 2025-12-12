using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions; 
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class ApplyModifierHandlerTests
{
    private readonly ILogger<ApplyModifierHandler> _loggerMock;
    private readonly IModifierDefinitionRepository _repoMock;
    private readonly IStatCalculationService _statServiceMock;
    private readonly ApplyModifierHandler _handler;

    private readonly GameState _dummyGameState;

    public ApplyModifierHandlerTests()
    {
        _loggerMock = Substitute.For<ILogger<ApplyModifierHandler>>();
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _statServiceMock = Substitute.For<IStatCalculationService>();

        _handler = new ApplyModifierHandler(
            _loggerMock,
            _repoMock,
            _statServiceMock);

        _dummyGameState = new GameState();
    }

    [Fact]
    public void Apply_WithNewModifier_ShouldAddModifierToTargetList_AndLog()
    {
        // ARRANGE
        var modId = "MOD_ATTACK_UP";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Bless",
            Type = ModifierType.Bless
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition>
        {
            { modId, modDef }
        });

        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = modId,
            DurationInTurns = 3,
            TargetRuleId = "T_Self"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new BaseStats() };

        target.ActiveModifiers.ShouldBeEmpty();

        // Objeto para capturar logs
        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effectDef, source, target, _dummyGameState, actionResult);

        // ASSERT
        // 1. Lógica de Estado
        target.ActiveModifiers.Count.ShouldBe(1);
        var appliedMod = target.ActiveModifiers.First();
        appliedMod.DefinitionId.ShouldBe(modId);
        appliedMod.TurnsRemaining.ShouldBe(3);
        appliedMod.CasterId.ShouldBe(source.Id);

        // 2. Lógica de Logs (Novo)
        actionResult.BattleLogEntries.ShouldContain(s => s.Contains("gained Bless"));
    }

    [Fact]
    public void Apply_ModifierWithStatusEffects_ShouldCopyEffectsToActiveModifier()
    {
        // ARRANGE
        var modId = "MOD_ICEBLOCK";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Ice Block",
            Type = ModifierType.Bless,
            GrantedStatusEffects = new List<StatusEffectType>
            {
                StatusEffectType.Invulnerable,
                StatusEffectType.Untargetable
            }
        };

        _repoMock.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var effectDef = new EffectDefinition
        {
            ModifierDefinitionId = modId,
            DurationInTurns = 1,
            TargetRuleId = "T_Self"
        };

        var source = new Combatant { Id = 1, Name = "Mage", BaseStats = new() };
        var target = new Combatant { Id = 1, Name = "Mage", BaseStats = new() };

        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effectDef, source, target, _dummyGameState, actionResult);

        // ASSERT
        var activeMod = target.ActiveModifiers.Single();

        activeMod.ActiveStatusEffects.ShouldNotBeNull();
        activeMod.ActiveStatusEffects.Count.ShouldBe(2);
        activeMod.ActiveStatusEffects.ShouldContain(StatusEffectType.Invulnerable);
        activeMod.ActiveStatusEffects.ShouldContain(StatusEffectType.Untargetable);
    }

    [Fact]
    public void Apply_WithBarrierDefinition_ShouldInitializeBarrierValueWithScaling()
    {
        // ARRANGE
        var modId = "MOD_SHIELD";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Mana Shield",
            Type = ModifierType.Bless,
            Barrier = new BarrierProperties
            {
                BaseAmount = 10,
                ScalingStat = StatType.Magic,
                ScalingFactor = 0.5f
            }
        };

        _repoMock.GetAllDefinitions()
            .Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = modId,
            DurationInTurns = 3,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Caster", BaseStats = new() };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new() };

        _statServiceMock.GetStatValue(source, StatType.Magic).Returns(20f);

        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effectDef, source, target, _dummyGameState, actionResult);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);
        var appliedMod = target.ActiveModifiers.First();

        // (20 * 0.5) + 10 = 20
        appliedMod.CurrentBarrierValue.ShouldBe(20f);
    }

    [Fact]
    public void Apply_WithExistingModifier_ShouldRefreshDuration_ResetBarrier_AndLogRefresh()
    {
        // ARRANGE
        var modId = "MOD_SHIELD";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Shield",
            Barrier = new BarrierProperties { BaseAmount = 50 },
            GrantedStatusEffects = new List<StatusEffectType> { StatusEffectType.Stun }
        };
        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var effectDef = new EffectDefinition
        {
            ModifierDefinitionId = modId,
            DurationInTurns = 5,
            TargetRuleId = "T_TestTarget"
        };
        var source = new Combatant { Id = 1, BaseStats = new(), Name = "Test_Combatant" };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new() };

        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = modId,
            TurnsRemaining = 1,
            CasterId = 99,
            CurrentBarrierValue = 10,
            ActiveStatusEffects = new List<StatusEffectType>()
        });

        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effectDef, source, target, _dummyGameState, actionResult);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);
        var refreshedMod = target.ActiveModifiers.First();

        refreshedMod.TurnsRemaining.ShouldBe(5);
        refreshedMod.CurrentBarrierValue.ShouldBe(50f);
        refreshedMod.ActiveStatusEffects.ShouldContain(StatusEffectType.Stun);

        // Verificação do Log de Refresh
        actionResult.BattleLogEntries.ShouldContain(s => s.Contains("was refreshed"));
    }

    [Fact]
    public void Apply_WithMissingModifierId_ShouldLogWarningAndDoNothing()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = null,
            TargetRuleId = "T_Self"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new BaseStats() };

        var actionResult = new CombatActionResult();

        // ACT
        _handler.Apply(effectDef, source, target, _dummyGameState, actionResult);

        // ASSERT
        target.ActiveModifiers.ShouldBeEmpty();

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}