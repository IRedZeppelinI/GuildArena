using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Infrastructure.Persistence.Json;
using GuildArena.Infrastructure.Persistence.Redis;
using GuildArena.Infrastructure.Persistence.Repositories;
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

        //  Redis como Singleton.        
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection)
        );
        // 3. Regista os repositórios
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