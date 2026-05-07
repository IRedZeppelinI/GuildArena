using GuildArena.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Encounter;

[Authorize]
[Route("api/[controller]")]
public class EncounterController : BaseApiController
{
    private readonly IEncounterService _encounterService;

    public EncounterController(IEncounterService encounterService)
    {
        _encounterService = encounterService;
    }

    [HttpGet]
    public IActionResult GetAvailableEncounters()
    {
        var result = _encounterService.GetAvailableEncounters();

        // HandleResult converte o Result no JSON final de forma standard
        return HandleResult(result);
    }
}