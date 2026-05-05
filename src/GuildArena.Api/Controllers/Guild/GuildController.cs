using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Shared.DTOs;
using GuildArena.Shared.DTOs.GuildAndHeroes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Guild;

[Authorize]
[Route("api/[controller]")]
public class GuildController : BaseApiController
{
    private readonly IGuildRepository _guildRepo;
    private readonly ICurrentUserService _currentUser;

    public GuildController(
        IGuildRepository guildRepo,
        ICurrentUserService currentUser)
    {
        _guildRepo = guildRepo;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves the roster of heroes for the authenticated player's guild.
    /// </summary>
    [HttpGet("my-roster")]
    public async Task<ActionResult<List<HeroDto>>> GetMyRoster()
    {
        if (!_currentUser.GuildId.HasValue)
        {
            return BadRequest("User is not associated with any guild.");
        }

        var heroes = await _guildRepo.GetAllHeroesAsync(_currentUser.GuildId.Value);

        var dtos = heroes.Select(h => new HeroDto
        {
            Id = h.Id,
            DefinitionId = h.CharacterDefinitionId,
            CurrentLevel = h.CurrentLevel
        }).ToList();

        return Ok(dtos);
    }
}