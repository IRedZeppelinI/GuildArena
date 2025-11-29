using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using GuildArena.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GuildArena.Core.UnitTests.Combat.Handlers;

public class GenerateResourceHandlerTests
{
    private readonly IEssenceService _essenceServiceMock;
    private readonly ILogger<ManipulateEssenceHandler> _loggerMock;
    private readonly ManipulateEssenceHandler _handler;
    private readonly GameState _gameState;

    public GenerateResourceHandlerTests()
    {
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _loggerMock = Substitute.For<ILogger<ManipulateEssenceHandler>>();
        _handler = new ManipulateEssenceHandler(_essenceServiceMock, _loggerMock);

        // Setup básico do GameState com 2 jogadores
        var p1 = new CombatPlayer { PlayerId = 1 };
        var p2 = new CombatPlayer { PlayerId = 2 };
        _gameState = new GameState { Players = new List<CombatPlayer> { p1, p2 } };
    }

    [Fact]
    public void Apply_Channeling_ShouldGiveEssenceToCaster()
    {
        // ARRANGE
        // Cenário: Channeling (Self-Cast). O Source e o Target são o mesmo.
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self", // Required prop
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Mind, Amount = 1 }
            }
        };

        var caster = new Combatant { Id = 10, OwnerId = 1, Name = "Mage", BaseStats = new BaseStats() };

        // ACT
        _handler.Apply(effect, caster, caster, _gameState); // Target = Caster

        // ASSERT
        // Deve adicionar 1 Mind ao Player 1
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 1),
            EssenceType.Mind,
            1
        );
    }

    [Fact]
    public void Apply_GiftToAlly_ShouldGiveEssenceToAllyOwner()
    {
        // ARRANGE
        // Cenário: Transferir Mana. Caster (P1) dá mana ao Aliado (P1).
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Ally", // Required prop
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Vigor, Amount = 2 }
            }
        };

        var caster = new Combatant { Id = 10, OwnerId = 1, Name = "Support", BaseStats = new BaseStats() };
        var allyTarget = new Combatant { Id = 11, OwnerId = 1, Name = "Warrior", BaseStats = new BaseStats() };

        // ACT
        _handler.Apply(effect, caster, allyTarget, _gameState);

        // ASSERT
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 1),
            EssenceType.Vigor,
            2
        );
    }

    //cursedGift está como exemplo de habilidade que tem drawBack de oferecer essence ao inimigo
    [Fact]
    public void Apply_CursedGift_ShouldGiveEssenceToEnemy()
    {
        // ARRANGE
        // Cenário: Uma habilidade que dá mana ao INIMIGO (trade-off).
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Enemy", // Required prop
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Shadow, Amount = 1 }
            }
        };

        var caster = new Combatant { Id = 10, OwnerId = 1, Name = "Warlock", BaseStats = new BaseStats() };
        var enemyTarget = new Combatant { Id = 20, OwnerId = 2, Name = "Paladin", BaseStats = new BaseStats() };

        // ACT
        _handler.Apply(effect, caster, enemyTarget, _gameState);

        // ASSERT
        // O Player 2 (dono do alvo) deve receber a essence
        _essenceServiceMock.Received(1).AddEssence(
            Arg.Is<CombatPlayer>(p => p.PlayerId == 2),
            EssenceType.Shadow,
            1
        );
    }

    [Fact]
    public void Apply_MultipleEssences_ShouldCallAddEssenceMultipleTimes()
    {
        // ARRANGE
        // Cenário: Psylian Channeling (Gera 1 Mind + 1 Neutral)
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self", // Required prop
            EssenceManipulations = new List<EssenceAmount>
            {
                new EssenceAmount { Type = EssenceType.Mind, Amount = 1 },
                new EssenceAmount { Type = EssenceType.Neutral, Amount = 1 }
            }
        };

        var caster = new Combatant { Id = 10, OwnerId = 1, Name = "Psylian", BaseStats = new BaseStats() };

        // ACT
        _handler.Apply(effect, caster, caster, _gameState);

        // ASSERT
        _essenceServiceMock.Received(1).AddEssence(Arg.Any<CombatPlayer>(), EssenceType.Mind, 1);
        _essenceServiceMock.Received(1).AddEssence(Arg.Any<CombatPlayer>(), EssenceType.Neutral, 1);
    }

    [Fact]
    public void Apply_EmptyDefinition_ShouldLogWarningAndDoNothing()
    {
        // ARRANGE
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Self", // Required prop
            EssenceManipulations = new List<EssenceAmount>() // Lista vazia
        };
        var caster = new Combatant { Id = 1, OwnerId = 1, Name = "Caster", BaseStats = new() };

        // ACT
        _handler.Apply(effect, caster, caster, _gameState);

        // ASSERT
        _essenceServiceMock.DidNotReceive().AddEssence(Arg.Any<CombatPlayer>(), Arg.Any<EssenceType>(), Arg.Any<int>());

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("list is empty")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Apply_TargetWithoutPlayer_ShouldLogWarning()
    {
        // ARRANGE
        // Cenário: Usar skill num Mob Neutro (OwnerId = 0 ou -1) que não está na lista de Players
        var effect = new EffectDefinition
        {
            Type = EffectType.MANIPULATE_ESSENCE,
            TargetRuleId = "T_Mob", // Required prop
            EssenceManipulations = new List<EssenceAmount> { new EssenceAmount { Amount = 1 } }
        };

        var caster = new Combatant { Id = 1, OwnerId = 1, Name = "Caster", BaseStats = new() };
        var neutralMob = new Combatant { Id = 99, OwnerId = 0, Name = "Neutral Creep", BaseStats = new() };

        // O GameState só tem Player 1 e 2.

        // ACT
        _handler.Apply(effect, caster, neutralMob, _gameState);

        // ASSERT
        _essenceServiceMock.DidNotReceive().AddEssence(Arg.Any<CombatPlayer>(), Arg.Any<EssenceType>(), Arg.Any<int>());

        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("has no valid player")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}