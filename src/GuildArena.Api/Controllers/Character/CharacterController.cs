using GuildArena.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Character;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CharacterController : BaseApiController
{
    private readonly ICharacterService _characterService;

    public CharacterController(ICharacterService characterService)
    {
        _characterService = characterService;
    }

    /// <summary>
    /// Retrieves the static definition and base stats of a character (Hero/Mob).
    /// </summary>
    [HttpGet("{definitionId}")]
    public IActionResult GetCharacterDetails(string definitionId)
    {
        var result = _characterService.GetCharacterDetails(definitionId);

        return HandleResult(result);
    }
}