using GuildArena.Application.Quests.GetActiveQuests;
using GuildArena.Application.Quests.RerollQuest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Quests;

/// <summary>
/// Exposes endpoints for retrieving and managing daily quests.
/// </summary>
[Authorize]
[Route("api/[controller]")]
public class QuestController : BaseApiController
{
    private readonly IMediator _mediator;

    public QuestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns all active (and today's completed) quests for the current guild.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActiveQuests()
    {
        var query = new GetActiveQuestsQuery();
        var result = await _mediator.Send(query);
        return HandleResult(result);
    }

    /// <summary>
    /// Re‑rolls the specified active quest (once per day).
    /// </summary>
    [HttpPost("{questId}/reroll")]
    public async Task<IActionResult> RerollQuest(int questId)
    {
        var command = new RerollQuestCommand { QuestId = questId };
        var result = await _mediator.Send(command);
        return HandleResult(result);
    }
}