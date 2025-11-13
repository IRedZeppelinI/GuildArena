using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat;

public class CombatEngineTests
{    
    private readonly ILogger<CombatEngine> _engineLoggerMock;

    public CombatEngineTests()
    {
        _engineLoggerMock = Substitute.For<ILogger<CombatEngine>>();
    }

    [Fact]
    public void ExecuteAbility_WithSupportedEffect_ShouldCallCorrectHandler()
    {
        //  ARRANGE
        // Criar MOCK do handler 
        var mockHandler = Substitute.For<IEffectHandler>();
        mockHandler.SupportedType.Returns(EffectType.DAMAGE);

        
        var engine = new CombatEngine(new[] { mockHandler }, _engineLoggerMock);

        var ability = new AbilityDefinition
        {
            Id = "TEST_ABILITY",
            Name = "Test",
            Effects = new List<EffectDefinition>
            {
                new() { Type = EffectType.DAMAGE }
            }
        };

        
        var source = new Combatant
        {
            Id = 1,
            Name = "A",
            CalculatedStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "B",
            CalculatedStats = new BaseStats()
        };

        // ACT
        engine.ExecuteAbility(ability, source, target);

        // ASSERT
        // Verifie uso método Apply
        mockHandler.Received(1).Apply(
            Arg.Is<EffectDefinition>(e => e.Type == EffectType.DAMAGE),
            source,
            target
        );
    }

    [Fact]
    public void ExecuteAbility_WithUnknownHandler_ShouldNotCrashAndLogWarning()
    {
        // ARRANGE        
        var engine = new CombatEngine(Enumerable.Empty<IEffectHandler>(), _engineLoggerMock);

        var ability = new AbilityDefinition
        {
            Id = "HEAL_ABILITY",
            Name = "Heal",
            Effects = new List<EffectDefinition> { new() { Type = EffectType.HEAL } }
        };

        var attacker = new Combatant
        {
            Id = 1,
            Name = "Hero",
            CalculatedStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            Name = "Mob",
            CurrentHP = 50,
            CalculatedStats = new BaseStats()
        };

        // ACT
        engine.ExecuteAbility(ability, attacker, target);

        // ASSERT
        // O estado não mudou (porque não havia handlers)
        target.CurrentHP.ShouldBe(50);

        // Verificae se o log de aviso foi disparado
        _engineLoggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o != null && o.ToString()!.Contains("No IEffectHandler found for EffectType HEAL")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}