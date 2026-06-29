using GuildArena.Api.Controllers;
using GuildArena.Application.Abstractions;
using GuildArena.Application.Account.DeleteAccount;
using GuildArena.Domain.Entities;
using GuildArena.Shared.DTOs.Identity;
using GuildArena.Shared.Requests;
using MediatR;
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
    private readonly IMediator _mediator;

    public AccountController(ICurrentUserService currentUserService,
        SignInManager<ApplicationUser> signInManager, IMediator mediator)
    {
        _currentUserService = currentUserService;
        _signInManager = signInManager;
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the currently authenticated user's context.
    /// The Blazor WebAssembly client calls this to reconstruct its local auth state.
    /// </summary>
    [HttpGet("me")]
    [Authorize] 
    public ActionResult<UserInfoDto> GetCurrentUser()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        // Parse the custom claim
        var emailConfirmedClaim = User.FindFirstValue("EmailConfirmed");
        bool isEmailConfirmed = bool.TryParse(emailConfirmedClaim, out var confirmed) && confirmed;

        return Ok(new UserInfoDto
        {
            Id = _currentUserService.UserId!,
            Email = email,
            GuildId = _currentUserService.GuildId,
            Roles = roles,
            IsEmailConfirmed = isEmailConfirmed 
        });
    }


    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok();
    }


    /// <summary>
    /// Permanently deletes the authenticated user's account.
    /// </summary>
    [HttpPost("delete")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        var command = new DeleteAccountCommand { Password = request.Password };
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            // Se apagou com sucesso, limpa os cookies imediatamente
            await _signInManager.SignOutAsync();
            return Ok();
        }

        return HandleResult(result);
    }
}