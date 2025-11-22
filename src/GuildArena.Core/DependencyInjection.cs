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
        services.AddScoped<IDamageModificationService, DamageModificationService>();
        services.AddScoped<ICooldownCalculationService, CooldownCalculationService>();

        services.AddScoped<ITurnManagerService, TurnManagerService>();
        services.AddScoped<IEssenceService, EssenceService>();
        services.AddScoped<ICostCalculationService, CostCalculationService>();
        services.AddScoped<ITargetResolutionService, TargetResolutionService>();

        // Registar todos os Handlers de Efeito        
        services.AddScoped<IEffectHandler, DamageEffectHandler>();
        services.AddScoped<IEffectHandler, ApplyModifierHandler>();
        // services.AddScoped<IEffectHandler, HealEffectHandler>();         

        return services;
    }
}