using System.Security.Claims;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GuildArena.Api.Services;

/// <summary>
/// Custom factory to generate claims during the login process.
/// Injects domain-specific context (like GuildId) into the encrypted HTTP-only cookie,
/// preventing the need to query the database on every subsequent request.
/// </summary>
public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly GuildArenaDbContext _dbContext;

    public CustomUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        GuildArenaDbContext dbContext)
        : base(userManager, roleManager, optionsAccessor)
    {
        _dbContext = dbContext;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        // 1. Let the base class add standard claims (UserId, Email, Roles)
        var identity = await base.GenerateClaimsAsync(user);

        // 2. Query the database to find the user's Guild
        var guild = await _dbContext.Guilds
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.ApplicationUserId == user.Id);

        // 3. If a Guild exists, stamp its ID securely into the cookie
        if (guild != null)
        {
            identity.AddClaim(new Claim("GuildId", guild.Id.ToString()));
        }

        return identity;
    }
}