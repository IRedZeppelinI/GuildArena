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
    
    private readonly ICooldownCalculationService _cooldownCalcServiceMock;

    public CombatEngineTests()
    {
        _engineLoggerMock = Substitute.For<ILogger<CombatEngine>>();
        
        _cooldownCalcServiceMock = Substitute.For<ICooldownCalculationService>();

         
        // serviço de cooldown devolve 0 por defeito.
        _cooldownCalcServiceMock.GetFinalCooldown(Arg.Any<Combatant>(), Arg.Any<AbilityDefinition>())
            .Returns(0);
    }

    [Fact]
    public void ExecuteAbility_WithSupportedEffect_ShouldCallCorrectHandler()
    {
        // ARRANGE
        var mockHandler = Substitute.For<IEffectHandler>();
        mockHandler.SupportedType.Returns(EffectType.DAMAGE);
                
        var engine = new CombatEngine(new[] { mockHandler }, _engineLoggerMock, _cooldownCalcServiceMock);

        var ability = new AbilityDefinition
        {
            Id = "TEST_ABILITY",
            Name = "Test", //
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
            BaseStats = new BaseStats() //
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
        var gameState = new GameState { Combatants = new List<Combatant> { source, target } }; //

        var targets = new AbilityTargets
        {
            SelectedTargets = new() { { "T_ENEMY", new List<int> { 5 } } } //
        };

        // ACT        
        engine.ExecuteAbility(gameState, ability, source, targets);

        // ASSERT
        mockHandler.Received(1).Apply(
            Arg.Is<EffectDefinition>(e => e.TargetRuleId == "T_ENEMY"),
            source,
            target
        );
    }

    [Fact]
    public void ExecuteAbility_WithUnknownHandler_ShouldNotCrashAndLogWarning()
    {
        // ARRANGE        
        var engine = new CombatEngine(Enumerable.Empty<IEffectHandler>(), _engineLoggerMock, _cooldownCalcServiceMock);

        var ability = new AbilityDefinition
        {
            Id = "HEAL_ABILITY",
            Name = "Heal", //
            TargetingRules = new() {
                new() { RuleId = "T_SELF", Type = TargetType.Self, Count = 1 }
            },
            Effects = new List<EffectDefinition> {
                new() { Type = EffectType.HEAL, TargetRuleId = "T_SELF" } //
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
        var targets = new AbilityTargets();

        // ACT
        engine.ExecuteAbility(gameState, ability, attacker, targets);

        // ASSERT
        attacker.CurrentHP.ShouldBe(50);
        _engineLoggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o != null && o.ToString()!.Contains("No IEffectHandler found for EffectType HEAL")), //
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    // --- CoolDowns ---
    [Fact]
    public void ExecuteAbility_WhenAbilityIsOnCooldown_ShouldReturnEarlyAndNotCallHandler()
    {
        // ARRANGE
        var mockHandler = Substitute.For<IEffectHandler>();
        mockHandler.SupportedType.Returns(EffectType.DAMAGE);

        var engine = new CombatEngine(new[] { mockHandler }, _engineLoggerMock, _cooldownCalcServiceMock);

        var ability = new AbilityDefinition { Id = "A1", Name = "Test Ability" };
        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            OwnerId = 1,
            BaseStats = new BaseStats()
        };

        // Colocar a habilidade em cooldown
        source.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "A1", TurnsRemaining = 2 }); //

        var target = new Combatant { Id = 2, Name = "Target", OwnerId = 2, BaseStats = new BaseStats() };
        var gameState = new GameState { Combatants = new List<Combatant> { source, target } };
        var targets = new AbilityTargets();

        // ACT
        engine.ExecuteAbility(gameState, ability, source, targets);

        // ASSERT
        // Não deve ter chamado o handler pois saiu mais cedo
        mockHandler.Received(0).Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>());

        // Deve ter logado o aviso
        _engineLoggerMock.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o != null && o.ToString()!.Contains($"on cooldown for {source.Id}. 2 turns remaining")), 
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
        
    [Fact]
    public void ExecuteAbility_WhenAbilityHasBaseCooldown_ShouldCallCooldownServiceAndAddActiveCooldown()
    {
        // ARRANGE
        var mockHandler = Substitute.For<IEffectHandler>(); // Handler (vazio, só para o engine não falhar)
        mockHandler.SupportedType.Returns(EffectType.DAMAGE);

        var engine = new CombatEngine(new[] { mockHandler }, _engineLoggerMock, _cooldownCalcServiceMock);

        var ability = new AbilityDefinition
        {
            Id = "A1_COOLDOWN",
            Name = "Test Cooldown",
            BaseCooldown = 5, // mock vai devolver 3 para representar um possivel buff
            Effects = new List<EffectDefinition>() // Sem efeitos para este teste
        };
        var source = new Combatant
        {
            Id = 1,
            Name = "Source",
            OwnerId = 1,
            BaseStats = new BaseStats()
        };
        var gameState = new GameState { Combatants = new List<Combatant> { source } };
        var targets = new AbilityTargets();

        // Setup: Dizer ao mock para devolver 3 turnos 
        _cooldownCalcServiceMock.GetFinalCooldown(source, ability).Returns(3);

        // ACT
        engine.ExecuteAbility(gameState, ability, source, targets);

        // ASSERT
        // 1. Verificamos se o motor CHAMOU o serviço de cálculo
        _cooldownCalcServiceMock.Received(1).GetFinalCooldown(source, ability);

        // 2. Verificamos se o motor ADICIONOU o cooldown à lista com o valor do serviço
        source.ActiveCooldowns.Count.ShouldBe(1); //
        var addedCd = source.ActiveCooldowns.First();
        addedCd.AbilityId.ShouldBe("A1_COOLDOWN");
        addedCd.TurnsRemaining.ShouldBe(3); // Usou o valor (3) do mock, não o BaseCooldown (5)
    }
}