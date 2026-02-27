using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using GuildArena.Infrastructure.Persistence.Json;
using GuildArena.Infrastructure.Persistence.Redis;
using GuildArena.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace GuildArena.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Adds and configures the Infrastructure services.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        //   Redis         
        var redisConnection = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnection))
        {            
            redisConnection = "localhost:6379";            
        }
               
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection)
        );

        // PostgreSQL
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<GuildArenaDbContext>(options =>
            options.UseNpgsql(dbConnectionString));

        // 3. Identity (Auth)
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            // Regras relaxadas para desenvolvimento
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
        })
            .AddEntityFrameworkStores<GuildArenaDbContext>()
            .AddDefaultTokenProviders();

        // Repositórios
        //redis
        services.AddScoped<ICombatStateRepository, RedisCombatStateRepository>();

        //jsons
        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();
        services.AddSingleton<IAbilityDefinitionRepository, JsonAbilityDefinitionRepository>();
        services.AddSingleton<IRaceDefinitionRepository, JsonRaceDefinitionRepository>();
        services.AddSingleton<ICharacterDefinitionRepository, JsonCharacterDefinitionRepository>();
        services.AddSingleton<IEncounterDefinitionRepository, JsonEncounterDefinitionRepository>();

        //SQL
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        


        return services;
    }
}