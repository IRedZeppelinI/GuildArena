using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.StartCombat;
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
        // Nota: O PlayerId não vem do request, é resolvido internamente pelo Handler via Token.
        var command = new StartPveCombatCommand
        {
            EncounterId = request.EncounterId,
            HeroInstanceIds = request.HeroInstanceIds
        };

        try
        {
            var combatId = await _mediator.Send(command);
            return Ok(new { combatId });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{combatId}/end-turn")]
    public async Task<IActionResult> EndTurn(string combatId)
    {
        // TODO: Este endpoint também deve validar se o user é o dono do turno no futuro.
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