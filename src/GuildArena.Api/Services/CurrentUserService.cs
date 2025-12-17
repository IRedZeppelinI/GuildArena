using GuildArena.Application.Abstractions;
using System.Security.Claims;

namespace GuildArena.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            // Lógica Final (Comentada para referência):
            // var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            // if (int.TryParse(userIdClaim?.Value, out int userId)) return userId;
            // return null;

            // Lógica Temporária para Desenvolvimento (Hardcoded):
            // Isto permite-te testar sem configurar JWT agora            
            return 1;
        }
    }
}