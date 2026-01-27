using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Actions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Gameplay;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class ManipulateEssenceHandler : IEffectHandler
{
    private readonly IEssenceService _essenceService;
    private readonly ILogger<ManipulateEssenceHandler> _logger;
    private readonly IBattleLogService _battleLog;

    public ManipulateEssenceHandler(
        IEssenceService essenceService,
        ILogger<ManipulateEssenceHandler> logger,
        IBattleLogService battleLog)
    {
        _essenceService = essenceService;
        _logger = logger;
        _battleLog = battleLog;
    }

    public EffectType SupportedType => EffectType.MANIPULATE_ESSENCE;

    public void Apply(
        EffectDefinition def,
        Combatant source,
        Combatant target,
        GameState gameState,
        CombatActionResult actionResult)
    {
        if (def.EssenceManipulations == null || def.EssenceManipulations.Count == 0)
        {            
            _logger.LogWarning("ManipulateEssence effect executed but list is empty.");
            return;
        }

        var beneficiaryPlayerId = target.OwnerId;
        var player = gameState.Players.FirstOrDefault(p => p.PlayerId == beneficiaryPlayerId);

        if (player == null)
        {            
            _logger.LogWarning("Target {TargetName} has no valid player. Essence manipulation ignored.", target.Name);
            return;
        }

        foreach (var manipulation in def.EssenceManipulations)
        {
            _essenceService.AddEssence(player, manipulation.Type, manipulation.Amount);

            // --- BATTLE LOG ---
            //Alterado para utilizar apenas battleLog de EssenceService. Apagar após testes
            //if (manipulation.Amount > 0)
            //{
            //    _battleLog.Log(
            //        $"{target.Name} gained {manipulation.Amount} {manipulation.Type} Essence.");
            //}
            //else
            //{
            //    _battleLog.Log(
            //        $"{target.Name} lost {Math.Abs(manipulation.Amount)} {manipulation.Type} Essence.");
            //}

            // App Log
            _logger.LogInformation(
                "Manipulated essence for Player {PlayerId}: {Amount} {Type}.",
                player.PlayerId, manipulation.Amount, manipulation.Type);
        }
    }
}