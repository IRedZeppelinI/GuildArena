using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class ManipulateEssenceHandler : IEffectHandler
{
    private readonly IEssenceService _essenceService;
    private readonly ILogger<ManipulateEssenceHandler> _logger;

    public ManipulateEssenceHandler(
        IEssenceService essenceService,
        ILogger<ManipulateEssenceHandler> logger)
    {
        _essenceService = essenceService;
        _logger = logger;
    }

    public EffectType SupportedType => EffectType.MANIPULATE_ESSENCE;

    public void Apply(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        GameState gameState,
        CombatActionResult actionResult)
    {
        if (def.EssenceManipulations == null || def.EssenceManipulations.Count == 0) return;

        var beneficiaryPlayerId = target.OwnerId;
        var player = gameState.Players.FirstOrDefault(p => p.PlayerId == beneficiaryPlayerId);

        if (player == null) return;

        foreach (var manipulation in def.EssenceManipulations)
        {
            _essenceService.AddEssence(player, manipulation.Type, manipulation.Amount);

            // --- BATTLE LOG ---
            if (manipulation.Amount > 0)
            {
                actionResult.AddBattleLog(
                    $"{target.Name} gained {manipulation.Amount} {manipulation.Type} Essence.");
            }
            else
            {
                actionResult.AddBattleLog(
                    $"{target.Name} lost {Math.Abs(manipulation.Amount)} {manipulation.Type} Essence.");
            }

            // App Log
            _logger.LogInformation(
                "Manipulated essence for Player {PlayerId}: {Amount} {Type}.",
                player.PlayerId, manipulation.Amount, manipulation.Type);
        }
    }
}