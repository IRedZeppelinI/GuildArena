using GuildArena.Api.Controllers;
using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Shared.DTOs.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GuildArena.Api.Controllers.Account;

[Route("api/[controller]")]
public class AccountController : BaseApiController
{
    private readonly ICurrentUserService _currentUserService;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(ICurrentUserService currentUserService,
        SignInManager<ApplicationUser> signInManager)
    {
        _currentUserService = currentUserService;
        _signInManager = signInManager;
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

        // NOVO: Lê as roles do Cookie no lado do servidor
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new UserInfoDto
        {
            Id = _currentUserService.UserId!,
            Email = email,
            GuildId = _currentUserService.GuildId,
            Roles = roles // NOVO: Envia para o cliente
        });
    }


    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }
}