using GuildArena.Application.Tavern.GetTavernHero;
using GuildArena.Application.Tavern.GetTavernShop;
using GuildArena.Application.Tavern.PurchaseHero;
using GuildArena.Shared.DTOs.Shop;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Tavern;

[Authorize]
[Route("api/[controller]")]
public class TavernController : BaseApiController
{
    private readonly IMediator _mediator;

    public TavernController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the full state of the tavern shop for the current guild.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTavernShop()
    {
        var query = new GetTavernShopQuery();
        var result = await _mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Purchases a hero for the current guild.
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> PurchaseHero([FromBody] PurchaseHeroRequest request)
    {
        var command = new PurchaseHeroCommand
        {
            HeroId = request.HeroId
        };
        var result = await _mediator.Send(command);
        return HandleResult(result);
    }

    /// <summary>
    /// Returns the tavern state for a single hero (purchase status, cost, conditions).
    /// Used by the hero details page to render the purchase section without depending on the full shop list.
    /// </summary>
    [HttpGet("{definitionId}")]
    public async Task<IActionResult> GetTavernHeroInfo(string definitionId)
    {
        var query = new GetTavernHeroQuery { DefinitionId = definitionId };
        var result = await _mediator.Send(query);
        return HandleResult(result);
    }
}