using GuildArena.Api.Mappers;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Targeting;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Domain.ValueObjects.Targeting;
using Shouldly;

namespace GuildArena.Api.UnitTests.Mappers;

public class CombatStateMapperTests
{
    [Fact]
    public void MapToDto_ShouldMapComplexProperties_AndMergeCooldownsCorrectly()
    {
        // ARRANGE
        var ability = new AbilityDefinition
        {
            Id = "ABIL_TEST",
            Name = "Test Strike",
            TargetingRules = new List<TargetingRule>
            {
                new TargetingRule { RuleId = "T1", Type = TargetType.Enemy, Count = 1 }
            }
        };

        var combatant = new Combatant
        {
            Id = 101,
            OwnerId = 1,
            Name = "Hero",
            RaceId = "RACE_HUMAN",
            BaseStats = new BaseStats { MaxActions = 2 },
            Abilities = new List<AbilityDefinition> { ability }
        };

        // Adicionar um cooldown ativo para a habilidade acima
        combatant.ActiveCooldowns.Add(new ActiveCooldown { AbilityId = "ABIL_TEST", TurnsRemaining = 3 });

        // Adicionar um modifier que foi castado por outro (ex: Taunt)
        combatant.ActiveModifiers.Add(new ActiveModifier { DefinitionId = "MOD_TAUNT", CasterId = 999 });

        var state = new GameState
        {
            CurrentTurnNumber = 5,
            CurrentPlayerId = 1,
            Combatants = new List<Combatant> { combatant },
            Players = new List<CombatPlayer> { new CombatPlayer { PlayerId = 1 } }
        };

        // ACT
        var dto = CombatStateMapper.MapToDto(state);

        // ASSERT
        dto.ShouldNotBeNull();
        dto.CurrentTurnNumber.ShouldBe(5);

        var combatantDto = dto.Combatants.First();
        combatantDto.MaxActions.ShouldBe(2);

        // Validar a Lógica de Fusão de Cooldown
        var abilityDto = combatantDto.Abilities.First();
        abilityDto.CurrentCooldownTurns.ShouldBe(3, "The mapper failed to merge the active cooldowns into the ability DTO.");

        // Validar os novos campos de Targeting
        abilityDto.TargetingRules.Count.ShouldBe(1);
        abilityDto.TargetingRules.First().Type.ShouldBe(TargetType.Enemy);

        // Validar o CasterId no Modifier
        combatantDto.ActiveModifiers.First().CasterId.ShouldBe(999);
    }
}