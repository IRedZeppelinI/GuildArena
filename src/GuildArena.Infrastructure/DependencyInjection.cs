using GuildArena.Application.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Infrastructure.Persistence.Json;
using GuildArena.Infrastructure.Persistence.Redis;
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
        //  Configuração do Redis 

        // 1. Obtém a connection string
        var redisConnection = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnection))
        {            
            redisConnection = "localhost:6379";            
        }

        //  Regista o IConnectionMultiplexer como Singleton.
        // para ser partilhada por toda a app.
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection)
        );
        // 3. Regista os repositórios
        //redis
        services.AddScoped<ICombatStateRepository, RedisCombatStateRepository>();

        //jsons
        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();
        services.AddSingleton<IAbilityDefinitionRepository, JsonAbilityDefinitionRepository>();


        return services;
    }
}