using GuildArena.Application.Combat.EndTurn;
using GuildArena.Application.Combat.StartCombat;
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


    /// <summary>
    /// Initializes a new combat instance (PvE demo) and returns its unique ID.
    /// </summary>
    /// <returns>The GUID of the created combat.</returns>
    [HttpPost("start")]
    public async Task<IActionResult> StartCombat([FromBody] StartCombatRequest request)
    {
        var command = new StartCombatCommand
        {
            PlayerId = request.PlayerId,
            OpponentId = request.OpponentId
        };

        var combatId = await _mediator.Send(command);
        return Ok(new { combatId });
    }


    /// <summary>
    /// Ends current  turn from specific combat.
    /// </summary>
    /// <param name="combatId">Combat GUID.</param>
    [HttpPost("{combatId}/end-turn")]
    public async Task<IActionResult> EndTurn(string combatId)
    {
        // 1. Criar o Comando 
        var command = new EndTurnCommand
        {
            CombatId = combatId
            // TODO: Obter o PlayerId do token JWT e adicionar ao comando
        };

        try
        {
            // 2. Enviar para o MediatR (que vai chamar o EndTurnCommandHandler)
            await _mediator.Send(command);

            // 3. Retornar Sucesso (200 OK)
            return Ok();
        }
        catch (Exception ex)
        {
            // (Num cenário real, usaríamos um Middleware de Tratamento de Erros global
            // em vez de try-catch aqui, mas para começar serve)
            //TODO Implementar global error 
            return BadRequest(new { error = ex.Message });
        }
    }

    public class StartCombatRequest
    {
        public int PlayerId { get; set; }
        public int OpponentId { get; set; }
    }
}

