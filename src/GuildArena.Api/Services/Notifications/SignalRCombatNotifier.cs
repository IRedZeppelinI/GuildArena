using GuildArena.Api.Hubs;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;
using Microsoft.AspNetCore.SignalR;

namespace GuildArena.Api.Services.Notifications;

/// <summary>
/// Implementation of ICombatNotifier that uses SignalR to push updates to connected clients.
/// </summary>
public class SignalRCombatNotifier : ICombatNotifier
{
    private readonly IHubContext<CombatHub> _hubContext;
    private readonly ILogger<SignalRCombatNotifier> _logger;

    public SignalRCombatNotifier(IHubContext<CombatHub> hubContext, ILogger<SignalRCombatNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendBattleLogsAsync(string combatId, List<string> logs)
    {
        if (logs == null || !logs.Any()) return;

        _logger.LogInformation("Broadcasting {Count} battle logs to Combat {CombatId}", logs.Count, combatId);

        // Envia para todos os clientes que fizeram "JoinCombat" neste combatId
        // O cliente Blazor terá de estar à escuta do evento "ReceiveBattleLogs"
        await _hubContext.Clients.Group(combatId).SendAsync("ReceiveBattleLogs", logs);
    }

    public async Task SendGameStateUpdateAsync(string combatId, GameState state)
    {
        _logger.LogInformation("Broadcasting GameState update for Combat {CombatId}", combatId);

        var dto = MapToDto(state);

        // O cliente Blazor terá de estar à escuta do evento "ReceiveGameStateUpdate"
        await _hubContext.Clients.Group(combatId).SendAsync("ReceiveGameStateUpdate", dto);
    }

    // --- MAPPING LOGIC (Domain -> DTO) ---
    // Optámos por mapeamento manual para manter o controlo absoluto do que é enviado para o cliente.

    private GameStateDto MapToDto(GameState state)
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
                EssencePool = new Dictionary<Domain.Enums.Resources.EssenceType, int>(p.EssencePool)
            }).ToList(),
            Combatants = state.Combatants.Select(MapCombatantToDto).ToList()
        };
    }

    private CombatantDto MapCombatantToDto(Combatant c)
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
            MaxActions = (int)c.BaseStats.MaxActions, // O Blazor não tem StatService, mandamos já calculado
            Position = c.Position,

            // Map Abilities with current cooldown injected
            SpecialAbility = c.SpecialAbility != null ? MapAbility(c.SpecialAbility, c.ActiveCooldowns) : null,
            Abilities = c.Abilities.Select(a => MapAbility(a, c.ActiveCooldowns)).ToList(),

            // Map Modifiers
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

    private AbilitySummaryDto MapAbility(Domain.Definitions.AbilityDefinition def, List<Domain.ValueObjects.State.ActiveCooldown> activeCooldowns)
    {
        // Verifica se esta habilidade está na lista de cooldowns do combatente
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