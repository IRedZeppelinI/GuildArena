using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Domain.Enums;
using GuildArena.Shared.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CombatController : ControllerBase
{
    private readonly IMediator _mediator;

    public CombatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("start-pve")]
    public async Task<IActionResult> StartPveCombat([FromBody] StartPveRequest request)
    {
        // --- 1. LÓGICA DE TESTE VS REAL ---

        // Se PlayerId for 0, assumimos modo de desenvolvimento/teste
        bool isTestMode = request.PlayerId <= 0;        
        int humanPlayerId = isTestMode ? 1 : request.PlayerId;

        // A AI/Sistema é sempre 0
        int aiPlayerId = 0;

        // Validação
        if (!isTestMode && humanPlayerId == aiPlayerId)
        {
            return BadRequest("Invalid Player ID. ID cannot be 0 (Reserved for System).");
        }

        // --- 2. CONFIGURAÇÃO DA EQUIPA DO JOGADOR ---

        List<StartCombatCommand.HeroSetup> playerTeam;

        if (isTestMode && !request.HeroIds.Any())
        {
            // MODO TESTE: Se não enviou heróis, Garret default
            playerTeam = new List<StartCombatCommand.HeroSetup>
            {
                new() { CharacterDefinitionId = "HERO_GARRET", InitialLevel = 1 }
            };
        }
        else
        {
            // MODO REAL (ou Teste com IDs específicos): Usamos o que veio no request
            playerTeam = request.HeroIds.Select(id => new StartCombatCommand.HeroSetup
            {
                CharacterDefinitionId = id,
                InitialLevel = 1, // No futuro virá da DB 
                LoadoutModifierIds = new List<string>() // Virá do request no futuro
            }).ToList();
        }

        var playerParticipant = new StartCombatCommand.Participant
        {
            PlayerId = humanPlayerId,
            Type = CombatPlayerType.Human,
            Team = playerTeam
        };

        // ---  CONFIGURAÇÃO DA AI ---

        //  No futuro, chamare_encounterService.GetMobs(zoneId)
        //  hardcoded para testes.
        var aiParticipant = new StartCombatCommand.Participant
        {
            PlayerId = aiPlayerId,
            Type = CombatPlayerType.AI,
            Team = new List<StartCombatCommand.HeroSetup>
            {
                new() { CharacterDefinitionId = "MOB_BANDIT_RECRUIT", InitialLevel = 1 },
                new() { CharacterDefinitionId = "MOB_BANDIT_RECRUIT", InitialLevel = 1 }
            }
        };

        // --- 4. EXECUÇÃO ---

        var command = new StartCombatCommand
        {
            Participants = new List<StartCombatCommand.Participant>
            {
                playerParticipant,
                aiParticipant
            }
        };

        var combatId = await _mediator.Send(command);
        return Ok(new { combatId });
    }

    [HttpPost("{combatId}/end-turn")]
    public async Task<IActionResult> EndTurn(string combatId)
    {
        var command = new EndTurnCommand { CombatId = combatId };
        try
        {
            await _mediator.Send(command);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}