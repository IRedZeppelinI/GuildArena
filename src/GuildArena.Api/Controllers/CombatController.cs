using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Shared.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CombatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CombatController> _logger; 

    public CombatController(IMediator mediator, ILogger<CombatController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("start-pve")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartPveCombat([FromBody] StartPveRequest request)
    {
        _logger.LogInformation
            ("Request received to start PvE Combat. Encounter: {EncounterId}", request.EncounterId);

        var command = new StartPveCombatCommand
        {
            EncounterId = request.EncounterId,
            HeroInstanceIds = request.HeroInstanceIds
        };

        try
        {
            var combatId = await _mediator.Send(command);
            _logger.LogInformation("PvE Combat started successfully. ID: {CombatId}", combatId);
            return Ok(new { combatId });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized attempt to start combat: {Message}", ex.Message);
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Start combat failed (Not Found): {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error starting combat.");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{combatId}/end-turn")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EndTurn(string combatId)
    {
        _logger.LogInformation("Request received to End Turn for Combat: {CombatId}", combatId);

        var command = new EndTurnCommand { CombatId = combatId };
        try
        {
            await _mediator.Send(command);
            _logger.LogInformation("Turn ended successfully for Combat: {CombatId}", combatId);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized end turn attempt: {Message}", ex.Message);
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("End turn failed (Combat not found): {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("End turn forbidden (Logic): {Message}", ex.Message);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error ending turn.");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{combatId}/execute-ability")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]     // Argumentos inválidos
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]   // Sem login
    [ProducesResponseType(StatusCodes.Status403Forbidden)]      // Turno errado / Boneco errado
    [ProducesResponseType(StatusCodes.Status404NotFound)]       // Combate não encontrado
    public async Task<IActionResult> ExecuteAbility(
        string combatId,
        [FromBody] ExecuteAbilityRequest request)
    {
        // Garantir consistência entre URL e Body
        if (request.CombatId != combatId)
        {
            return BadRequest(new { error = "Combat ID mismatch between URL and Body." });
        }

        var command = new ExecuteAbilityCommand
        {
            CombatId = request.CombatId,
            SourceId = request.SourceId,
            AbilityId = request.AbilityId,
            TargetSelections = request.TargetSelections,
            Payment = request.Payment
        };

        try
        {
            // Recebemos os logs (temporariamente) para debug
            var logs = await _mediator.Send(command);

            return Ok(new { logs });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized ability execution: {Message}", ex.Message);
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Erros de input (Habilidade não existe, Source não encontrada)
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Erros de lógica de jogo (Turno errado, Sem mana, Stunned)
            // Retornamos 400 ou 403 dependendo da nuance, mas 400 com mensagem clara serve.
            _logger.LogWarning("Ability execution logic error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing ability.");
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }
}