using GuildArena.Api.Mappers;
using GuildArena.Application.Dungeons.EnterDungeonStage;
using GuildArena.Application.Dungeons.ForfeitDungeon;
using GuildArena.Application.Dungeons.GetActiveCampState;
using GuildArena.Application.Dungeons.GetAvailableDungeons;
using GuildArena.Application.Dungeons.StartDungeon;
using GuildArena.Shared.Requests;
using GuildArena.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Dungeon;

/// <summary>
/// Exposes endpoints for the dungeon system: listing dungeons, managing an active run, and entering combat.
/// </summary>
[Authorize]
[Route("api/[controller]")]
public class DungeonController : BaseApiController
{
    private readonly IMediator _mediator;
    private readonly ICombatStateMapper _combatMapper;

    public DungeonController(IMediator mediator, ICombatStateMapper combatMapper)
    {
        _mediator = mediator;
        _combatMapper = combatMapper;
    }

    /// <summary>
    /// Returns all available dungeons for the player.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailableDungeons()
    {
        var result = await _mediator.Send(new GetAvailableDungeonsQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Gets the current camp state (active run details) for the player's guild.
    /// </summary>
    [HttpGet("camp")]
    public async Task<IActionResult> GetActiveCamp()
    {
        var result = await _mediator.Send(new GetActiveCampStateQuery());
        return HandleResult(result);
    }

    /// <summary>
    /// Starts a new dungeon run with the selected heroes.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartDungeon([FromBody] StartDungeonRequest request)
    {
        var command = new StartDungeonCommand
        {
            DungeonId = request.DungeonId,
            HeroInstanceIds = request.HeroInstanceIds
        };
        var result = await _mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Enters the current stage of the active dungeon run (starts combat).
    /// </summary>
    [HttpPost("enter-stage")]
    public async Task<IActionResult> EnterStage()
    {
        var command = new EnterDungeonStageCommand();
        var result = await _mediator.Send(command);

        if (result.IsFailure)
            return HandleResult(result);

        var response = new StartCombatResponse
        {
            CombatId = result.Value.CombatId,
            InitialLogs = result.Value.InitialLogs,
            InitialState = _combatMapper.MapToDto(result.Value.InitialState)
        };
        return Ok(response);
    }

    /// <summary>
    /// Abandons the current dungeon run.
    /// </summary>
    [HttpPost("forfeit")]
    public async Task<IActionResult> Forfeit()
    {
        var command = new ForfeitDungeonCommand();
        var result = await _mediator.Send(command);
        return HandleResult(result);
    }
}