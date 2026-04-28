using System.Security.Claims;
using GuildArena.Application.Abstractions;

namespace GuildArena.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? GuildId
    {
        get
        {
            // TEMPORARY DEVELOPMENT LOGIC
            // Hardcoded to Guild 1 to allow testing the combat engine without requiring a full login flow.
            return 1;

            // FINAL PRODUCTION LOGIC
            // var guildClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("GuildId");
            // return int.TryParse(guildClaim, out var guildId) ? guildId : null;
        }
    }
}