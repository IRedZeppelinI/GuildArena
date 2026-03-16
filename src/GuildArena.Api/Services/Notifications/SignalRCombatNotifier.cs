using GuildArena.Api.Hubs;
using GuildArena.Api.Mappers;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Domain.Gameplay;
using Microsoft.AspNetCore.SignalR;

namespace GuildArena.Api.Services.Notifications;

public class SignalRCombatNotifier : ICombatNotifier
{
    private readonly IHubContext<CombatHub> _hubContext;
    private readonly ILogger<SignalRCombatNotifier> _logger;
    private readonly ICombatStateMapper _mapper;

    public SignalRCombatNotifier(
        IHubContext<CombatHub> hubContext,
        ILogger<SignalRCombatNotifier> logger,
        ICombatStateMapper mapper)
    {
        _hubContext = hubContext;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task SendBattleLogsAsync(string combatId, List<string> logs)
    {
        if (logs == null || !logs.Any()) return;

        _logger.LogInformation("Broadcasting {Count} battle logs to Combat {CombatId}", logs.Count, combatId);

        await _hubContext.Clients.Group(combatId).SendAsync("ReceiveBattleLogs", logs);
    }

    public async Task SendGameStateUpdateAsync(string combatId, GameState state)
    {
        _logger.LogInformation("Broadcasting GameState update for Combat {CombatId}", combatId);

        var dto = _mapper.MapToDto(state);

        await _hubContext.Clients.Group(combatId).SendAsync("ReceiveGameStateUpdate", dto);
    }
}