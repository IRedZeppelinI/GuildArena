using GuildArena.Core.Combat.Abstractions;
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

public class DamageEffectHandlerTests
{
    private readonly IStatCalculationService _statCalculationServiceMock;
    private readonly ILogger<DamageEffectHandler> _loggerMock;
    private readonly DamageEffectHandler _handler;

    public DamageEffectHandlerTests()
    {
        // ARRANGE Global 
        _statCalculationServiceMock = Substitute.For<IStatCalculationService>();
        _loggerMock = Substitute.For<ILogger<DamageEffectHandler>>();

        // Criamos a classe que vamos testar, injetando os mocks
        _handler = new DamageEffectHandler(_statCalculationServiceMock, _loggerMock);
    }

    [Fact]
    public void Apply_WithStandardDamage_ShouldReduceTargetHP()
    {
        //  ARRANGE         
        var effectDef = new EffectDefinition
        {
            Type = EffectType.DAMAGE,
            ScalingStat = StatType.Attack,
            ScalingFactor = 1.0f,
            BaseAmount = 0
        };

        // Criar os combatentes
        var source = new Combatant { Id = 1, Name = "Hero", CalculatedStats = new BaseStats() };
        var target = new Combatant { Id = 2, Name = "Mob", CurrentHP = 50, CalculatedStats = new BaseStats() };

        // Configurar os Mocks 
        _statCalculationServiceMock.GetStatValue(source, StatType.Attack).Returns(10f);
        _statCalculationServiceMock.GetStatValue(target, StatType.Defense).Returns(2f);

        // ACT 
        _handler.Apply(effectDef, source, target);

        // ASSERT 
        // Dano = 10 (Ataque) - 2 (Defesa) = 8
        // HP Final = 50 - 8 = 42
        target.CurrentHP.ShouldBe(42);
    }
}