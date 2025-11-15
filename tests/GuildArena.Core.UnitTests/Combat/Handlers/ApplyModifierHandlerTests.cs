using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class ApplyModifierHandlerTests
{
    private readonly ILogger<ApplyModifierHandler> _loggerMock;
    private readonly ApplyModifierHandler _handler;

    public ApplyModifierHandlerTests()
    {
        _loggerMock = Substitute.For<ILogger<ApplyModifierHandler>>();
        _handler = new ApplyModifierHandler(_loggerMock);
    }

    [Fact]
    public void Apply_WithNewModifier_ShouldAddModifierToTargetList()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = "MOD_ATTACK_UP",
            DurationInTurns = 3,
            TargetRuleId = "T_Self" 
        };

        var source = new Combatant { Id = 1, Name = "Source", CalculatedStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CalculatedStats = new BaseStats() };

        target.ActiveModifiers.ShouldBeEmpty();

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);

        var appliedMod = target.ActiveModifiers.First();
        appliedMod.DefinitionId.ShouldBe("MOD_ATTACK_UP");
        appliedMod.TurnsRemaining.ShouldBe(3);
        appliedMod.CasterId.ShouldBe(source.Id);
    }

    [Fact]
    public void Apply_WithExistingModifier_ShouldRefreshDuration()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = "MOD_ATTACK_UP",
            DurationInTurns = 5, 
            TargetRuleId = "T_Self" 
        };

        var source = new Combatant { Id = 1, Name = "Source", CalculatedStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", CalculatedStats = new BaseStats() };

        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = "MOD_ATTACK_UP",
            TurnsRemaining = 1,
            CasterId = 99
        });

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);

        var refreshedMod = target.ActiveModifiers.First();
        refreshedMod.DefinitionId.ShouldBe("MOD_ATTACK_UP");
        refreshedMod.TurnsRemaining.ShouldBe(5); 
        refreshedMod.CasterId.ShouldBe(99);
    }
}