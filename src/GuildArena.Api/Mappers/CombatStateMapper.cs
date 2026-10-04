using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Resources;
using GuildArena.Domain.Enums.Stats;
using GuildArena.Domain.Gameplay;
using GuildArena.Domain.ValueObjects.State;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Api.Mappers;

/// <summary>
/// Responsible for translating the internal domain GameState into secure DTOs for the frontend,
/// pre-calculating targeting rules, affordability, and effect math for UI tooltips.
/// </summary>
public class CombatStateMapper : ICombatStateMapper
{
    private readonly ITargetResolutionService _targetService;
    private readonly IEssenceService _essenceService;
    private readonly IEffectTooltipService _tooltipService;
    private readonly IStatCalculationService _statService;
    private readonly IModifierDefinitionRepository _modifierRepository;

    public CombatStateMapper(
        ITargetResolutionService targetService,
        IEssenceService essenceService,
        IEffectTooltipService tooltipService,
        IStatCalculationService statService,
        IModifierDefinitionRepository modifierRepository)
    {
        _targetService = targetService;
        _essenceService = essenceService;
        _tooltipService = tooltipService;
        _statService = statService;
        _modifierRepository = modifierRepository;
    }

    public GameStateDto MapToDto(GameState state)
    {
        var modifiers = _modifierRepository.GetAllDefinitions();
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
            Combatants = state.Combatants.Select(c => MapCombatantToDto(c, state, modifiers)).ToList()
        };
    }

    private CombatantDto MapCombatantToDto(Combatant c, GameState state, IReadOnlyDictionary<string, ModifierDefinition> modifiers)
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
            //MaxActions = (int)c.BaseStats.MaxActions,
            Position = c.Position,

            MaxActions = (int)_statService.GetStatValue(c, StatType.MaxActions),
            Attack = (int)_statService.GetStatValue(c, StatType.Attack),
            Defense = (int)_statService.GetStatValue(c, StatType.Defense),
            Agility = (int)_statService.GetStatValue(c, StatType.Agility),
            Magic = (int)_statService.GetStatValue(c, StatType.Magic),
            MagicDefense = (int)_statService.GetStatValue(c, StatType.MagicDefense),

            SpecialAbility = c.SpecialAbility != null
                ? MapAbility(c.SpecialAbility, c, state) : null,

            Abilities = c.Abilities
                .Select(a => MapAbility(a, c, state)).ToList(),

            ActiveModifiers = c.ActiveModifiers.Select(m => MapModifier(m, modifiers)).ToList()
        };
    }

    private static ActiveModifierDto MapModifier(ActiveModifier modifier, IReadOnlyDictionary<string, ModifierDefinition> definitions)
    {
        definitions.TryGetValue(modifier.DefinitionId, out var definition);
        return new ActiveModifierDto
        {
            DefinitionId = modifier.DefinitionId,
            Name = definition?.Name,
            Description = definition?.Description,
            CasterId = modifier.CasterId,
            TurnsRemaining = modifier.TurnsRemaining,
            StackCount = modifier.StackCount,
            CurrentBarrierValue = modifier.CurrentBarrierValue,
            ActiveStatusEffects = modifier.ActiveStatusEffects.ToList()
        };
    }

    private AbilitySummaryDto MapAbility(AbilityDefinition def, Combatant source, GameState state)
    {
        var cd = source.ActiveCooldowns.FirstOrDefault(x => x.AbilityId == def.Id);
        var ownerPlayer = state.Players.FirstOrDefault(p => p.PlayerId == source.OwnerId);

        bool isMyTurn = state.CurrentPlayerId == source.OwnerId;
        bool hasEssence = ownerPlayer != null && _essenceService.HasEnoughEssence(ownerPlayer, def.Costs);
        bool hasHP = source.CurrentHP > def.HPCost;
        //bool hasAP = (source.ActionsTakenThisTurn + def.ActionPointCost) <= source.BaseStats.MaxActions;
        int currentMaxActions = (int)_statService.GetStatValue(source, StatType.MaxActions);
        bool hasAP = (source.ActionsTakenThisTurn + def.ActionPointCost) <= currentMaxActions;

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
            Tags = def.Tags.ToList(),

            // Generate the dynamic math for the UI tooltip using the LIVE combatant
            Effects = def.Effects.Select(e => _tooltipService.GeneratePreview(source, e)).ToList(),

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
