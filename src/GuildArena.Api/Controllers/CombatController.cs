using GuildArena.Api.Mappers;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.ExchangeEssence;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Shared.Requests;
using GuildArena.Shared.Responses;
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

    /// <summary>
    /// Initializes a new PvE combat session.
    /// </summary>    
    [HttpPost("start-pve")]
    [ProducesResponseType(typeof(StartCombatResponse), StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartPveCombat([FromBody] StartPveRequest request)
    {
        _logger.LogInformation(
            "Request received to start PvE Combat. Encounter: {EncounterId}", request.EncounterId);

        var command = new StartPveCombatCommand
        {
            EncounterId = request.EncounterId,
            HeroInstanceIds = request.HeroInstanceIds
        };

        try
        {
            // 1. Recebe o resultado da camada Application (StartCombatResult)
            var result = await _mediator.Send(command);

            _logger.LogInformation(
                "PvE Combat started successfully. ID: {CombatId}. Waiting for client to connect to SignalR.",
                result.CombatId);

                // 2. Mapeia para o contrato público do projeto Shared (StartCombatResponse)
                var response = new StartCombatResponse
                {
                    CombatId = result.CombatId,
                    InitialLogs = result.InitialLogs,
                    InitialState = CombatStateMapper.MapToDto(result.InitialState)
                };

                // 3. Devolve ao Blazor
                return Ok(response);
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


    /// <summary>
    /// Ends the current player's turn and triggers the next phase (e.g., AI turn).
    /// </summary>
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
            _logger.LogInformation("Turn ended successfully for Combat: {CombatId}. Updates sent via SignalR.", combatId);
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

    /// <summary>
    /// Executes a combat ability.
    /// </summary>
    [HttpPost("{combatId}/execute-ability")]
    [ProducesResponseType(StatusCodes.Status200OK)] 
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteAbility(
        string combatId,
        [FromBody] ExecuteAbilityRequest request)
    {
        // Log explícito da intenção recebida
        _logger.LogInformation(
            "Request received: Execute Ability {AbilityId} from Source {SourceId} in Combat {CombatId}.",
            request.AbilityId, request.SourceId, combatId);

        if (request.CombatId != combatId)
        {
            _logger.LogWarning("Combat ID mismatch: URL {UrlId} vs Body {BodyId}.", combatId, request.CombatId);
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
            await _mediator.Send(command);

            _logger.LogInformation(
                "Ability {AbilityId} processed successfully for Combat {CombatId}. Updates sent via SignalR.",
                request.AbilityId, combatId);

            return Ok(); 
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized ability execution: {Message}", ex.Message);
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Combat session {CombatId} not found.", combatId);
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid argument for ability execution: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Erros de lógica de jogo (Turno errado, Sem mana, Stunned)
            _logger.LogWarning("Ability execution logic error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing ability {AbilityId} in combat {CombatId}.", request.AbilityId, combatId);
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }



    /// <summary>
    /// Exchanges two existing essences for one essence of the player's choice.
    /// </summary>[HttpPost("{combatId}/exchange-essence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExchangeEssence(
        string combatId,
        [FromBody] ExchangeEssenceRequest request)
    {
        if (request.CombatId != combatId)
        {
            return BadRequest(new { error = "Combat ID mismatch between URL and Body." });
        }

        var command = new ExchangeEssenceCommand
        {
            CombatId = request.CombatId,
            EssenceToSpend = request.EssenceToSpend,
            EssenceToGain = request.EssenceToGain
        };

        try
        {
            await _mediator.Send(command);
            return Ok();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error exchanging essence.");
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }
}