using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

/// <summary>
/// Handles immediate manipulation of player essence pools (Add, Remove, Random).
/// </summary>
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

    public void Apply(EffectDefinition def, Combatant source, Combatant target, GameState gameState)
    {
        // Validação de Segurança
        if (def.EssenceManipulations == null || def.EssenceManipulations.Count == 0)
        {
            _logger.LogWarning("ManipulateEssence effect executed but list is empty.");
            return;
        }

        // Determinar player de target
        var beneficiaryPlayerId = target.OwnerId;
        var player = gameState.Players.FirstOrDefault(p => p.PlayerId == beneficiaryPlayerId);

        if (player == null)
        {
            _logger.LogWarning("Target {TargetName} has no valid player. Essence manipulation ignored.", target.Name);
            return;
        }

        // Aplicar Manipulações
        foreach (var manipulation in def.EssenceManipulations)
        {            
            _essenceService.AddEssence(player, manipulation.Type, manipulation.Amount);

            _logger.LogInformation(
                "Manipulated essence for Player {PlayerId}: {Amount} {Type} (via Target {TargetName}).",
                manipulation.Amount, manipulation.Type, player.PlayerId, target.Name);
        }
    }
}