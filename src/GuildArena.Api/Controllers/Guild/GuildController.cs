using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Shared.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.Guild;

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
        // Delega TUDO para o serviço (incluindo validações)
        var result = await _guildService.CreateGuildAsync(
            _currentUser.UserId!,
            _currentUser.GuildId,
            request.GuildName);

        // Se falhou (ex: nome inválido, já tem guilda), o BaseApiController trata do Erro!
        if (result.IsFailure)
        {
            return HandleResult(result);
        }

        // Lógica puramente Web: Refrescar o Cookie de Autenticação
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

        // HandleResult devolve Ok(result.Value) se for sucesso, ou ProblemDetails se falhar
        return HandleResult(result);
    }
}