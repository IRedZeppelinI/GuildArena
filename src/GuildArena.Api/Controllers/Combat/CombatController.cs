using GuildArena.Api.Mappers;
using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.ExchangeEssence;
using GuildArena.Application.Combat.ExecuteAbility;
using GuildArena.Application.Combat.StartCombat;
using GuildArena.Application.Combat.Surrender;
using GuildArena.Shared.Requests;
using GuildArena.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers;

[Route("api/[controller]")]
public class CombatController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly ILogger<CombatController> _logger;
    private readonly ICombatStateMapper _mapper;

    public CombatController(
        IMediator mediator,
        ILogger<CombatController> logger,
        ICombatStateMapper mapper)
    {
        _mediator = mediator;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Initializes a new PvE combat session.
    /// </summary>    
    [HttpPost("start-encounter")]
    [ProducesResponseType(typeof(StartCombatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartEncounterCombat([FromBody] StartEncounterRequest request)
    {
        _logger.LogInformation("Request received to start PvE Combat. Encounter: {EncounterId}", request.EncounterId);

        var command = new StartEncounterCombatCommand
        {
            EncounterId = request.EncounterId,
            HeroInstanceIds = request.HeroInstanceIds
        };

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to start combat: {Message}", result.Error.Message);
            return HandleResult(result);
        }

        var response = new StartCombatResponse
        {
            CombatId = result.Value.CombatId,
            InitialLogs = result.Value.InitialLogs,
            InitialState = _mapper.MapToDto(result.Value.InitialState)
        };

        return Ok(response);
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

        var result = await _mediator.Send(command);

        return HandleResult(result);
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
        string combatId, [FromBody] ExecuteAbilityRequest request)
    {
        if (request.CombatId != combatId)
        {
            return BadRequest(new { detail = "Combat ID mismatch between URL and Body." });
        }

        var command = new ExecuteAbilityCommand
        {
            CombatId = request.CombatId,
            SourceId = request.SourceId,
            AbilityId = request.AbilityId,
            TargetSelections = request.TargetSelections,
            Payment = request.Payment
        };

        var result = await _mediator.Send(command);

        return HandleResult(result);
    }

    /// <summary>
    /// Exchanges two existing essences for one essence of the player's choice.
    /// </summary>
    [HttpPost("{combatId}/exchange-essence")]
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
            return BadRequest(new { detail = "Combat ID mismatch between URL and Body." });
        }

        var command = new ExchangeEssenceCommand
        {
            CombatId = request.CombatId,
            EssenceToSpend = request.EssenceToSpend,
            EssenceToGain = request.EssenceToGain
        };

        var result = await _mediator.Send(command);

        return HandleResult(result);
    }

    /// <summary>
    /// Desiste do combate atual.
    /// </summary>
    [HttpPost("{combatId}/surrender")]
    public async Task<IActionResult> Surrender(string combatId)
    {
        _logger.LogInformation("Request received to Surrender for Combat: {CombatId}", combatId);

        var command = new SurrenderCommand(combatId);
        var result = await _mediator.Send(command);

        return HandleResult(result);
    }
}