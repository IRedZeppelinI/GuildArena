using System.Security.Claims;
using GuildArena.Api.Controllers;
using GuildArena.Application.Abstractions;
using GuildArena.Shared.DTOs.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Account;

[Route("api/[controller]")]
public class AccountController : BaseApiController
{
    private readonly ICurrentUserService _currentUserService;

    public AccountController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Returns the currently authenticated user's context.
    /// The Blazor WebAssembly client calls this to reconstruct its local auth state.
    /// </summary>
    [HttpGet("me")]
    [Authorize] // Only logged-in users can hit this
    public ActionResult<UserInfoDto> GetCurrentUser()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        return Ok(new UserInfoDto
        {
            Id = _currentUserService.UserId!,
            Email = email,
            GuildId = _currentUserService.GuildId
        });
    }
}