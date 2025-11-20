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
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Configuração do Redis ---

        // 1. Obtém a connection string
        var redisConnection = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnection))
        {
            // Para desenvolvimento local, podemos apontar para o Docker
            redisConnection = "localhost:6379";
            // NOTA: Em produção, isto deve vir da configuração!
        }

        // 2. Regista o IConnectionMultiplexer como Singleton.
        // Isto é crucial. A ligação ao Redis deve ser partilhada por toda a app.
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection)
        );

        // 3. Regista o nosso repositório
        services.AddScoped<ICombatStateRepository, RedisCombatStateRepository>();
        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();

        // (No futuro, o IModifierDefinitionRepository seria registado aqui)

        return services;
    }
}