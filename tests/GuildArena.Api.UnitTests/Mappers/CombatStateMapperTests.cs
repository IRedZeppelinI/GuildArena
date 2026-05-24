using GuildArena.Api.Mappers;
using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.Resources;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.Targeting;
using GuildArena.Shared.DTOs.Combat;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GuildArena.Api.UnitTests.Mappers;

public class CombatStateMapperTests
{
    private readonly ITargetResolutionService _targetServiceMock;
    private readonly IEssenceService _essenceServiceMock;
    private readonly IEffectTooltipService _tooltipServiceMock;
    private readonly IStatCalculationService _statServiceMock; 
    private readonly CombatStateMapper _mapper;

    public CombatStateMapperTests()
    {
        _targetServiceMock = Substitute.For<ITargetResolutionService>();
        _essenceServiceMock = Substitute.For<IEssenceService>();
        _tooltipServiceMock = Substitute.For<IEffectTooltipService>();
        _statServiceMock = Substitute.For<IStatCalculationService>();
        // Ensure the mock returns a valid empty object to avoid null references in tests
        _tooltipServiceMock.GeneratePreview(Arg.Any<Combatant>(), Arg.Any<EffectDefinition>())
            .Returns(new AbilityEffectSummaryDto());
        // Ensinamos o Mock a ler as MaxActions que foram configuradas no bloco ARRANGE de cada teste!
        _statServiceMock.GetStatValue(Arg.Any<Combatant>(), Arg.Any<StatType>())
            .Returns(callInfo =>
            {
                var combatant = callInfo.Arg<Combatant>();
                var statType = callInfo.Arg<StatType>();

                if (statType == StatType.MaxActions)
                {
                    return combatant.BaseStats.MaxActions;
                }

                return 10f; 
            });
        _mapper = new CombatStateMapper(_targetServiceMock, _essenceServiceMock, _tooltipServiceMock, _statServiceMock);
    }

    [Fact]
    public void MapToDto_ShouldMapComplexProperties_AndCalculateRules()
    {
        // ARRANGE
        var rule = new TargetingRule { RuleId = "T1", Type = TargetType.Enemy, Count = 1 };

        var ability = new AbilityDefinition
        {
            Id = "ABIL_TEST",
            Name = "Test Strike",
            TargetingRules = new List<TargetingRule> { rule },
            // Adicionado um Efeito para o Mapper ter o que enviar para o TooltipService
            Effects = new List<EffectDefinition> { new EffectDefinition { TargetRuleId = "T1" } }
        };

        var combatant = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Hero",
            RaceId = "RACE_HUMAN",
            CurrentHP = 100,
            BaseStats = new BaseStats { MaxActions = 2 },
            Abilities = new List<AbilityDefinition> { ability }
        };

        combatant.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "ABIL_TEST", TurnsRemaining = 3 });
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_TAUNT", CasterId = 999 });

        var player = new CombatPlayer { PlayerId = 1 };

        var state = new GameState
        {
            CurrentTurnNumber = 5,
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { combatant },
            Players = new List<CombatPlayer> { player }
        };

        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(true);
        _targetServiceMock.GetValidCandidates(rule, combatant, state).Returns(new List<Combatant>
        {
            new Combatant { Id = 201, Name = "Enemy", RaceId = "X", BaseStats = new BaseStats() }
        });

        // ACT
        var dto = _mapper.MapToDto(state);

        // ASSERT
        dto.ShouldNotBeNull();
        dto.CurrentTurnNumber.ShouldBe(5);

        var combatantDto = dto.Combatants.First();
        combatantDto.MaxActions.ShouldBe(2);

        var abilityDto = combatantDto.Abilities.First();
        abilityDto.CurrentCooldownTurns.ShouldBe(3);
        abilityDto.IsAffordable.ShouldBeTrue();
        abilityDto.TargetingRules.Count.ShouldBe(1);
        abilityDto.TargetingRules.First().ValidTargetIds.ShouldContain(201);
        combatantDto.ActiveModifiers.First().CasterId.ShouldBe(999);

        // Verifica se chamou a Tooltip 
        abilityDto.Effects.ShouldNotBeNull();
        _tooltipServiceMock.ReceivedWithAnyArgs().GeneratePreview(default!, default!);
    }


    [Fact]
    public void MapToDto_WhenEssenceIsInsufficient_IsAffordableShouldBeFalse()
    {
        // ARRANGE
        var ability = new AbilityDefinition { Id = "A1", Name = "A1" };
        var combatant = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Hero",
            RaceId = "X",
            CurrentHP = 100,
            BaseStats = new BaseStats { MaxActions = 1 },
            Abilities = new List<AbilityDefinition> { ability }
        };
        var player = new CombatPlayer { PlayerId = 1 };
        var state = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { combatant },
            Players = new List<CombatPlayer> { player }
        };

        // O jogador NÃO TEM essence suficiente
        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(false);

        // ACT
        var dto = _mapper.MapToDto(state);

        // ASSERT
        var abilityDto = dto.Combatants.First().Abilities.First();
        abilityDto.IsAffordable.ShouldBeFalse("Ability should not be affordable without enough essence.");
    }

    [Fact]
    public void MapToDto_WhenHPIsInsufficient_IsAffordableShouldBeFalse()
    {
        // ARRANGE
        var ability = new AbilityDefinition { Id = "A1", Name = "A1", HPCost = 50 };
        var combatant = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Hero",
            RaceId = "X",
            CurrentHP = 50, // Tem exatamente 50 HP (não pode suicidar-se)
            BaseStats = new BaseStats { MaxActions = 1 },
            Abilities = new List<AbilityDefinition> { ability }
        };
        var player = new CombatPlayer { PlayerId = 1 };
        var state = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { combatant },
            Players = new List<CombatPlayer> { player }
        };

        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(true);

        // ACT
        var dto = _mapper.MapToDto(state);

        // ASSERT
        var abilityDto = dto.Combatants.First().Abilities.First();
        abilityDto.IsAffordable.ShouldBeFalse("Ability should not be affordable if it would kill the caster.");
    }
    [Fact]
    public void MapToDto_WhenAPIsInsufficient_IsAffordableShouldBeFalse()
    {
        // ARRANGE
        var ability = new AbilityDefinition { Id = "A1", Name = "A1", ActionPointCost = 1 };
        var combatant = new Combatant
        {
            Id = 1,
            OwnerId = 1,
            Name = "Hero",
            RaceId = "X",
            CurrentHP = 100,
            BaseStats = new BaseStats { MaxActions = 1 },
            ActionsTakenThisTurn = 1, // Já gastou a ação do turno
            Abilities = new List<AbilityDefinition> { ability }
        };
        var player = new CombatPlayer { PlayerId = 1 };
        var state = new GameState
        {
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { combatant },
            Players = new List<CombatPlayer> { player }
        };

        _essenceServiceMock.HasEnoughEssence(player, Arg.Any<List<EssenceAmount>>()).Returns(true);

        // ACT
        var dto = _mapper.MapToDto(state);

        // ASSERT
        var abilityDto = dto.Combatants.First().Abilities.First();
        abilityDto.IsAffordable.ShouldBeFalse("Ability should not be affordable if the combatant is out of Action Points.");
    }
}