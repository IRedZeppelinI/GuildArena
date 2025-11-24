using GuildArena.Core.Combat.Abstractions; // Para IStatCalculationService
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Abstractions.Repositories;
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
    private readonly IModifierDefinitionRepository _repoMock;     
    private readonly IStatCalculationService _statServiceMock;    
    private readonly ApplyModifierHandler _handler;

    public ApplyModifierHandlerTests()
    {
        _loggerMock = Substitute.For<ILogger<ApplyModifierHandler>>();
        _repoMock = Substitute.For<IModifierDefinitionRepository>();
        _statServiceMock = Substitute.For<IStatCalculationService>();

        _handler = new ApplyModifierHandler(
            _loggerMock,
            _repoMock,
            _statServiceMock);
    }

    [Fact]
    public void Apply_WithNewModifier_ShouldAddModifierToTargetList()
    {
        // ARRANGE
        var modId = "MOD_ATTACK_UP";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Buff",
            Type = ModifierType.BUFF
        };

        // Configurar o Repo para devolver a definição
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

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);

        var appliedMod = target.ActiveModifiers.First();
        appliedMod.DefinitionId.ShouldBe(modId);
        appliedMod.TurnsRemaining.ShouldBe(3);
        appliedMod.CasterId.ShouldBe(source.Id);
    }

    [Fact]
    public void Apply_WithBarrierDefinition_ShouldInitializeBarrierValueWithScaling()
    {
        // ARRANGE - Testar se a barreira é calculada (Base + Scaling)
        var modId = "MOD_SHIELD";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Mana Shield",
            Type = ModifierType.BUFF,
            Barrier = new BarrierProperties
            {
                BaseAmount = 10,
                ScalingStat = StatType.Magic,
                ScalingFactor = 0.5f // 50% do Magic
            }
        };

        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = modId,
            DurationInTurns = 3,
            TargetRuleId = "T_TestTarget"
        };

        var source = new Combatant { Id = 1, Name = "Caster", BaseStats = new() };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new() };

        // Configurar Stats do Caster (Magic = 20)
        // Valor esperado: 10 (Base) + (20 * 0.5) = 20
        _statServiceMock.GetStatValue(source, StatType.Magic).Returns(20f);

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);
        var appliedMod = target.ActiveModifiers.First();

        // Verifica se o valor inicial foi calculado corretamente
        appliedMod.CurrentBarrierValue.ShouldBe(20f);
    }

    [Fact]
    public void Apply_WithExistingModifier_ShouldRefreshDurationAndResetBarrier()
    {
        // ARRANGE
        var modId = "MOD_SHIELD";
        var modDef = new ModifierDefinition
        {
            Id = modId,
            Name = "Shield",
            Barrier = new BarrierProperties { BaseAmount = 50 }
        };
        _repoMock.GetAllDefinitions().Returns(new Dictionary<string, ModifierDefinition> { { modId, modDef } });

        var effectDef = new EffectDefinition { 
            ModifierDefinitionId = modId,
            DurationInTurns = 5,
            TargetRuleId = "T_TestTarget"
        };
        var source = new Combatant { Id = 1, BaseStats = new(), Name = "Test_Combatant" };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new() };

        // Simular modifier existente com barreira gasta (10 HP restantes)
        target.ActiveModifiers.Add(new ActiveModifier
        {
            DefinitionId = modId,
            TurnsRemaining = 1,
            CasterId = 99,
            CurrentBarrierValue = 10
        });

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.Count.ShouldBe(1);
        var refreshedMod = target.ActiveModifiers.First();

        refreshedMod.TurnsRemaining.ShouldBe(5); // Refreshed Duration
        refreshedMod.CurrentBarrierValue.ShouldBe(50f); // Reset Barrier to Max
    }

    [Fact]
    public void Apply_WithMissingModifierId_ShouldLogWarningAndDoNothing()
    {
        // ARRANGE
        var effectDef = new EffectDefinition
        {
            Type = EffectType.APPLY_MODIFIER,
            ModifierDefinitionId = null, // ID em falta
            TargetRuleId = "T_Self"
        };

        var source = new Combatant { Id = 1, Name = "Source", BaseStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Target", BaseStats = new BaseStats() };

        // ACT
        _handler.Apply(effectDef, source, target);

        // ASSERT
        target.ActiveModifiers.ShouldBeEmpty();

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(), // Aceita qualquer mensagem de warning
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}