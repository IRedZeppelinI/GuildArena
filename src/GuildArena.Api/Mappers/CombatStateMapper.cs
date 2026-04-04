using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Api.Mappers;

public class CombatStateMapper : ICombatStateMapper
{
    private readonly ITargetResolutionService _targetService;
    private readonly IEssenceService _essenceService;

    public CombatStateMapper(
        ITargetResolutionService targetService,
        IEssenceService essenceService)
    {
        _targetService = targetService;
        _essenceService = essenceService;
    }

    public GameStateDto MapToDto(GameState state)
    {
        return new GameStateDto
        {
            CurrentTurnNumber = state.CurrentTurnNumber,
            CurrentPlayerId = state.CurrentPlayerId,
            BackgroundId = state.BackgroundId,
            Players = state.Players.Select(p => new CombatPlayerDto
            {
                PlayerId = p.PlayerId,
                Name = p.Name,
                Type = p.Type,
                MaxTotalEssence = p.MaxTotalEssence,
                EssencePool = new Dictionary<EssenceType, int>(p.EssencePool)
            }).ToList(),
            Combatants = state.Combatants.Select(c => MapCombatantToDto(c, state)).ToList()
        };
    }

    private CombatantDto MapCombatantToDto(Combatant c, GameState state)
    {
        return new CombatantDto
        {
            Id = c.Id,
            DefinitionId = c.DefinitionId,
            OwnerId = c.OwnerId,
            Name = c.Name,
            RaceId = c.RaceId,
            MaxHP = c.MaxHP,
            CurrentHP = c.CurrentHP,
            ActionsTakenThisTurn = c.ActionsTakenThisTurn,
            MaxActions = (int)c.BaseStats.MaxActions,
            Position = c.Position,

            SpecialAbility = c.SpecialAbility != null
                ? MapAbility(c.SpecialAbility, c, state) : null,

            Abilities = c.Abilities
                .Select(a => MapAbility(a, c, state)).ToList(),

            ActiveModifiers = c.ActiveModifiers.Select(m => new ActiveModifierDto
            {
                DefinitionId = m.DefinitionId,
                CasterId = m.CasterId,
                TurnsRemaining = m.TurnsRemaining,
                StackCount = m.StackCount,
                CurrentBarrierValue = m.CurrentBarrierValue,
                ActiveStatusEffects = m.ActiveStatusEffects.ToList()
            }).ToList()
        };
    }

    private AbilitySummaryDto MapAbility(AbilityDefinition def, Combatant source, GameState state)
    {
        var cd = source.ActiveCooldowns.FirstOrDefault(x => x.AbilityId == def.Id);

        var ownerPlayer = state.Players.FirstOrDefault(p => p.PlayerId == source.OwnerId);

        // Verifica se o turno atual pertence ao dono desta carta
        bool isMyTurn = state.CurrentPlayerId == source.OwnerId;

        bool hasEssence = ownerPlayer != null && _essenceService.HasEnoughEssence(ownerPlayer, def.Costs);
        bool hasHP = source.CurrentHP > def.HPCost;
        bool hasAP = (source.ActionsTakenThisTurn + def.ActionPointCost) <= source.BaseStats.MaxActions;

        //  A habilidae só é "Affordable" se for o turno deste jogador
        bool isAffordable = isMyTurn && hasEssence && hasHP && hasAP;

        return new AbilitySummaryDto
        {
            Id = def.Id,
            Name = def.Name,
            Description = def.Description ?? string.Empty,
            ActionPointCost = def.ActionPointCost,
            BaseCooldown = def.BaseCooldown,
            HPCost = def.HPCost,
            Costs = def.Costs.ToDictionary(k => k.Type, v => v.Amount),
            CurrentCooldownTurns = cd?.TurnsRemaining ?? 0,
            IsAffordable = isAffordable, 

            TargetingRules = def.TargetingRules.Select(r => new TargetingRuleDto
            {
                RuleId = r.RuleId,
                Type = r.Type,
                Count = r.Count,
                Strategy = r.Strategy,
                ValidTargetIds = _targetService.GetValidCandidates(r, source, state)
                                               .Select(t => t.Id)
                                               .ToList()
            }).ToList()
        };
    }
}