using GuildArena.Api.Mappers;
using GuildArena.Api.Services;
using GuildArena.Api.Services.Notifications;
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;

namespace GuildArena.Api;

/// <summary>
/// Handles the registration of API-specific services, including Web Authentication, 
/// SignalR real-time communication, and HTTP context accessors.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. API Exclusive Services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICombatStateMapper, CombatStateMapper>();

        // 2. SignalR
        services.AddSignalR();
        services.AddScoped<ICombatNotifier, SignalRCombatNotifier>();

        // 3. Security & Identity (HTTP-Only Cookies via Identity API Endpoints)
        services.AddAuthorization();

        services.AddIdentityApiEndpoints<ApplicationUser>(options =>
        {
            configuration.GetSection("IdentityOptions").Bind(options);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<GuildArenaDbContext>();


        // REGISTO FACTORY CUSTOMIZADO para cookie de Guild
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomUserClaimsPrincipalFactory>();

        return services;
    }
}