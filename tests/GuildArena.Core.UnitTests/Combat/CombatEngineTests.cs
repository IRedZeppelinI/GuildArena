using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.ValueObjects;
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
    private readonly ILogger<CombatEngine> _logger;
    private readonly ICooldownCalculationService _cooldownMock;
    private readonly ICostCalculationService _costMock;
    private readonly IEssenceService _essenceMock;
    private readonly IEffectHandler _handlerMock;

    public CombatEngineTests()
    {
        _logger = Substitute.For<ILogger<CombatEngine>>();
        _cooldownMock = Substitute.For<ICooldownCalculationService>();
        _costMock = Substitute.For<ICostCalculationService>();
        _essenceMock = Substitute.For<IEssenceService>();
        _handlerMock = Substitute.For<IEffectHandler>();

        _handlerMock.SupportedType.Returns(EffectType.DAMAGE);
    }

    private CombatEngine CreateEngine(IEnumerable<IEffectHandler>? handlers = null)
    {
        var handlerList = handlers ?? new[] { _handlerMock };
        return new CombatEngine(
            handlerList,
            _logger,
            _cooldownMock,
            _costMock,
            _essenceMock);
    }

    [Fact]
    public void ExecuteAbility_HappyPath_ShouldPayCostsAndExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition
        {
            Id = "A1",
            Name = "Fireball",
            Effects = new() { new() { Type = EffectType.DAMAGE, TargetRuleId = "T1" } },
            TargetingRules = new() { new() { RuleId = "T1", Type = TargetType.Enemy, Count = 1 } }
        };

        // Dados do Combate - CORRIGIDO (Name e BaseStats)
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "P1",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };
        var target = new Combatant
        {
            Id = 2,
            OwnerId = 2,
            Name = "P2",
            CurrentHP = 100,
            BaseStats = new BaseStats()
        };

        var player = new CombatPlayer { PlayerId = 1 };
        var gameState = new GameState
        {
            Combatants = new() { source, target },
            Players = new() { player }
        };

        var targets = new AbilityTargets { SelectedTargets = new() { { "T1", new List<int> { 2 } } } };
        var payment = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 2 } };

        // --- CONFIGURAÇÃO DOS MOCKS ---

        // 1. Cooldown: OK
        _cooldownMock.GetFinalCooldown(source, ability).Returns(0);

        // 2. Custos Calculados
        var calculatedCost = new FinalAbilityCosts
        {
            HPCost = 10,
            EssenceCosts = new() { new() { Type = EssenceType.Vigor, Amount = 2 } }
        };
        _costMock.CalculateFinalCosts(player, ability, Arg.Any<List<Combatant>>())
            .Returns(calculatedCost);

        // 3. Essence Service: OK
        _essenceMock.HasEnoughEssence(player, calculatedCost.EssenceCosts).Returns(true);

        // ACT
        engine.ExecuteAbility(gameState, ability, source, targets, payment);

        // ASSERT
        _essenceMock.Received(1).PayEssence(player, payment);
        source.CurrentHP.ShouldBe(90); // 100 - 10
        _handlerMock.Received(1).Apply(Arg.Any<EffectDefinition>(), source, target);
    }

    [Fact]
    public void ExecuteAbility_OnCooldown_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "A1", Name = "Test" };

        // CORRIGIDO
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Source",
            BaseStats = new BaseStats()
        };

        source.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "A1", TurnsRemaining = 1 });

        var gameState = new GameState();
        var targets = new AbilityTargets();
        var payment = new Dictionary<EssenceType, int>();

        // ACT
        engine.ExecuteAbility(gameState, ability, source, targets, payment);

        // ASSERT
        _costMock.DidNotReceive().CalculateFinalCosts(Arg.Any<CombatPlayer>(), Arg.Any<AbilityDefinition>(), Arg.Any<List<Combatant>>());
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>());
    }

    [Fact]
    public void ExecuteAbility_NotEnoughHP_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "Suicide", Name = "Die" };

        // CORRIGIDO
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Source",
            BaseStats = new BaseStats(),
            CurrentHP = 10
        };

        var player = new CombatPlayer { PlayerId = 1 };
        var gameState = new GameState { Combatants = new() { source }, Players = new() { player } };

        var highCost = new FinalAbilityCosts { HPCost = 20, EssenceCosts = new() };
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>())
            .Returns(highCost);

        // ACT
        engine.ExecuteAbility(gameState, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>());

        _logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Not enough HP")),
            null, Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ExecuteAbility_NotEnoughEssence_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "Expensive", Name = "Rich" };

        // CORRIGIDO
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Source",
            BaseStats = new BaseStats(),
            CurrentHP = 100
        };

        var player = new CombatPlayer { PlayerId = 1 };
        var gameState = new GameState { Combatants = new() { source }, Players = new() { player } };

        var cost = new FinalAbilityCosts { EssenceCosts = new() { new() { Type = EssenceType.Light, Amount = 5 } } };
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>())
            .Returns(cost);

        _essenceMock.HasEnoughEssence(player, cost.EssenceCosts).Returns(false);

        // ACT
        engine.ExecuteAbility(gameState, ability, source, new AbilityTargets(), new Dictionary<EssenceType, int>());

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>());
    }

    [Fact]
    public void ExecuteAbility_PaymentMismatch_ShouldNotExecute()
    {
        // ARRANGE
        var engine = CreateEngine();
        var ability = new AbilityDefinition { Id = "Spell", Name = "Cast" };

        // CORRIGIDO
        var source = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Source",
            BaseStats = new BaseStats(),
            CurrentHP = 100
        };

        var player = new CombatPlayer { PlayerId = 1 };
        var gameState = new GameState { Combatants = new() { source }, Players = new() { player } };

        var invoice = new FinalAbilityCosts { EssenceCosts = new() { new() { Type = EssenceType.Vigor, Amount = 2 } } };
        _costMock.CalculateFinalCosts(Arg.Any<CombatPlayer>(), ability, Arg.Any<List<Combatant>>())
            .Returns(invoice);

        _essenceMock.HasEnoughEssence(player, invoice.EssenceCosts).Returns(true);

        var badPayment = new Dictionary<EssenceType, int> { { EssenceType.Vigor, 1 } };

        // ACT
        engine.ExecuteAbility(gameState, ability, source, new AbilityTargets(), badPayment);

        // ASSERT
        _handlerMock.DidNotReceive().Apply(Arg.Any<EffectDefinition>(), Arg.Any<Combatant>(), Arg.Any<Combatant>());

        _logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Payment provided does not match")),
            null, Arg.Any<Func<object, Exception?, string>>());
    }
}