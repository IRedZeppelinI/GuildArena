using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Shared.DTOs;
using GuildArena.Shared.DTOs.GuildAndHeroes;
using GuildArena.Shared.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Guild;

[Authorize]
[Route("api/[controller]")]
public class GuildController : BaseApiController
{
    private readonly IGuildRepository _guildRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public GuildController(
        IGuildRepository guildRepo,
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _guildRepo = guildRepo;
        _currentUser = currentUser;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateGuild([FromBody] CreateGuildRequest request)
    {
        if (_currentUser.GuildId.HasValue)
        {
            return BadRequest(new { detail = "User already has an active Guild." });
        }

        if (string.IsNullOrWhiteSpace(request.GuildName) || request.GuildName.Length < 3)
        {
            return BadRequest(new { detail = "Guild name must be at least 3 characters long." });
        }

        // 1. Criar a Guild e o Starter Pack
        await _guildRepo.CreateWithStarterPackAsync(_currentUser.UserId!, request.GuildName);

        // 2. Refrescar o Cookie de Autenticação para injetar GuildId
        var user = await _userManager.FindByIdAsync(_currentUser.UserId!);
        if (user != null)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        return Ok();
    }

    [HttpGet("my-roster")]
    public async Task<ActionResult<List<HeroDto>>> GetMyRoster()
    {
        if (!_currentUser.GuildId.HasValue)
        {
            return BadRequest(new { detail = "User is not associated with any guild." });
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