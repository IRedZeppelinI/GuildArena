using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Shared.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
public class GuildController : BaseApiController
{
    private readonly IGuildService _guildService;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public GuildController(
        IGuildService guildService,
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _guildService = guildService;
        _currentUser = currentUser;
        _userManager = userManager;
        _signInManager = signInManager;
    }
    [HttpPost("create")]
    public async Task<IActionResult> CreateGuild([FromBody] CreateGuildRequest request)
    {
        var result = await _guildService.CreateGuildAsync(
            _currentUser.UserId!,
            _currentUser.GuildId,
            request.GuildName);

        if (result.IsFailure)
        {
            return HandleResult(result);
        }

        var user = await _userManager.FindByIdAsync(_currentUser.UserId!);
        if (user != null)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        return Ok();
    }

    [HttpGet("my-roster")]
    public async Task<IActionResult> GetMyRoster()
    {
        var result = await _guildService.GetGuildRosterAsync(_currentUser.GuildId);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves the current user's guild profile and progression stats.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _guildService.GetGuildProfileAsync(_currentUser.UserId!);
        return HandleResult(result);
    }
}