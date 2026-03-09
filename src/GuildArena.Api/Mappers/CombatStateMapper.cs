using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Api.Mappers;

/// <summary>
/// Responsible for mapping the internal domain game state into the secure DTO consumed by the Blazor client.
/// Ensures sensitive logic and unneeded data are not exposed over the network.
/// </summary>
public static class CombatStateMapper
{
    /// <summary>
    /// Maps the root GameState to a GameStateDto.
    /// </summary>
    public static GameStateDto MapToDto(GameState state)
    {
        return new GameStateDto
        {
            CurrentTurnNumber = state.CurrentTurnNumber,
            CurrentPlayerId = state.CurrentPlayerId,
            Players = state.Players.Select(p => new CombatPlayerDto
            {
                PlayerId = p.PlayerId,
                Name = p.Name,
                Type = p.Type,
                MaxTotalEssence = p.MaxTotalEssence,
                EssencePool = new Dictionary<EssenceType, int>(p.EssencePool)
            }).ToList(),
            Combatants = state.Combatants.Select(MapCombatantToDto).ToList()
        };
    }

    private static CombatantDto MapCombatantToDto(Combatant c)
    {
        return new CombatantDto
        {
            Id = c.Id,
            OwnerId = c.OwnerId,
            Name = c.Name,
            RaceId = c.RaceId,
            MaxHP = c.MaxHP,
            CurrentHP = c.CurrentHP,
            ActionsTakenThisTurn = c.ActionsTakenThisTurn,
            MaxActions = (int)c.BaseStats.MaxActions,
            Position = c.Position,
            SpecialAbility = c.SpecialAbility != null ? MapAbility(c.SpecialAbility, c.ActiveCooldowns) : null,
            Abilities = c.Abilities.Select(a => MapAbility(a, c.ActiveCooldowns)).ToList(),
            ActiveModifiers = c.ActiveModifiers.Select(m => new ActiveModifierDto
            {
                DefinitionId = m.DefinitionId,
                TurnsRemaining = m.TurnsRemaining,
                StackCount = m.StackCount,
                CurrentBarrierValue = m.CurrentBarrierValue,
                ActiveStatusEffects = m.ActiveStatusEffects.ToList()
            }).ToList()
        };
    }

    private static AbilitySummaryDto MapAbility(AbilityDefinition def, List<ActiveCooldown> activeCooldowns)
    {
        var cd = activeCooldowns.FirstOrDefault(x => x.AbilityId == def.Id);
        return new AbilitySummaryDto
        {
            Id = def.Id,
            Name = def.Name,
            ActionPointCost = def.ActionPointCost,
            BaseCooldown = def.BaseCooldown,
            HPCost = def.HPCost,
            Costs = def.Costs.ToDictionary(k => k.Type, v => v.Amount),
            CurrentCooldownTurns = cd?.TurnsRemaining ?? 0
        };
    }
}