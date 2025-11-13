using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GuildArena.Core;

public static class DependencyInjection
{
    /// <summary>
    /// Adds and configures the Core (Business Logic) services.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Regista o Orquestrador Principal
        services.AddScoped<ICombatEngine, CombatEngine>();

        // Regista os Serviços Auxiliares
        services.AddScoped<IStatCalculationService, StatCalculationService>();

        // Regista todos os Handlers de Efeito
        // O .NET DI vai automaticamente injetar o IEnumerable<IEffectHandler> no CombatEngine
        services.AddScoped<IEffectHandler, DamageEffectHandler>();
        // services.AddScoped<IEffectHandler, HealEffectHandler>(); // (Quando o criares)
        // services.AddScoped<IEffectHandler, ApplyModifierHandler>(); // (Quando o criares)

        // ... (Regista outros serviços do Core, como IDungeonService, etc.)

        return services;
    }
}