using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

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
        // ARRANGE

        //  Criar Mock Handler
        var mockHandler = Substitute.For<IEffectHandler>();
        mockHandler.SupportedType.Returns(EffectType.DAMAGE);

        
        var engine = new CombatEngine(new[] { mockHandler }, _engineLoggerMock);

        
        var ability = new AbilityDefinition
        {
            Id = "TEST_ABILITY",
            Name = "Test",
            TargetingRules = new() {
                new() { RuleId = "T_ENEMY", Type = TargetType.Enemy, Count = 1 }
            },
            Effects = new() {
                new() { Type = EffectType.DAMAGE, TargetRuleId = "T_ENEMY" }
            }
        };

        
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Source",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 5,
            OwnerId = 2,
            Name = "Target",
            CurrentHP = 100,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState { Combatants = new List<Combatant> { source, target } };

        //  Criar o "Clique do Jogador" (O Mapa de Alvos)
        var targets = new AbilityTargets
        {
            SelectedTargets = new() { { "T_ENEMY", new List<int> { 5 } } } // Mapeia "T_ENEMY" para o ID 5
        };

        // ACT        
        engine.ExecuteAbility(gameState, ability, source, targets);

        // ASSERT
        // Verificar se chamou o apply com sucesso
        mockHandler.Received(1).Apply(
            Arg.Is<EffectDefinition>(e => e.TargetRuleId == "T_ENEMY"),
            source,
            target // O motor deve ter resolvido o ID 5 para o objeto "target"
        );
    }

    [Fact]
    public void ExecuteAbility_WithUnknownHandler_ShouldNotCrashAndLogWarning()
    {
        //  ARRANGE
        // Criar um motor SEM handlers 
        var engine = new CombatEngine(Enumerable.Empty<IEffectHandler>(), _engineLoggerMock);

        var ability = new AbilityDefinition
        {
            Id = "HEAL_ABILITY",
            Name = "Heal",
            TargetingRules = new() {
                new() { RuleId = "T_SELF", Type = TargetType.Self, Count = 1 }
            },
            Effects = new List<EffectDefinition> {
                new() { Type = EffectType.HEAL, TargetRuleId = "T_SELF" }
            }
        };

        var attacker = new Combatant
        {
            Id = 1,
            Name = "Hero",
            OwnerId = 1,
            CurrentHP = 50,
            MaxHP = 100,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState { Combatants = new List<Combatant> { attacker } };
        var targets = new AbilityTargets(); // Mapa vazio

        // ACT
        engine.ExecuteAbility(gameState, ability, attacker, targets);

        // ASSERT
        // O estado não mudou
        attacker.CurrentHP.ShouldBe(50);

        // Verifir se o log de aviso foi disparado
        _engineLoggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o != null && o.ToString()!.Contains("No IEffectHandler found for EffectType HEAL in Ability HEAL_ABILITY")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}