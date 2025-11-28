using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GuildArena.Core.Combat.Handlers;

public class GenerateResourceHandler : IEffectHandler
{
    private readonly IEssenceService _essenceService;
    private readonly ILogger<GenerateResourceHandler> _logger;

    public GenerateResourceHandler(
        IEssenceService essenceService,
        ILogger<GenerateResourceHandler> logger)
    {
        _essenceService = essenceService;
        _logger = logger;
    }

    public EffectType SupportedType => EffectType.GENERATE_RESOURCE;

    public void Apply(EffectDefinition def, Combatant source, Combatant target, GameState gameState)
    {
        // Validação
        if (def.GeneratedEssences == null || def.GeneratedEssences.Count == 0)
        {
            _logger.LogWarning("GenerateResource effect executed but 'GeneratedEssences' list is empty.");
            return;
        }

        
        var beneficiaryPlayerId = target.OwnerId;
        var player = gameState.Players.FirstOrDefault(p => p.PlayerId == beneficiaryPlayerId);

        if (player == null)
        {
            _logger.LogWarning("Target {TargetName} (Owner {OwnerId}) has no valid player in GameState. Essence generation ignored.",
                target.Name, target.OwnerId);
            return;
        }

        // Iterar sobre a lista e adicionar cada tipo de essence definido
        foreach (var essence in def.GeneratedEssences)
        {
            _essenceService.AddEssence(player, essence.Type, essence.Amount);

            _logger.LogInformation("Effect generated {Amount} {Type} for Player {PlayerId} (via Target {TargetName}).",
                essence.Amount, essence.Type, player.PlayerId, target.Name);
        }
    }
}