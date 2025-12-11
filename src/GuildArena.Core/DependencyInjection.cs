using GuildArena.Core.Combat;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Core.Combat.Factories;
using GuildArena.Core.Combat.Handlers;
using GuildArena.Core.Combat.Services;
using GuildArena.Domain.Abstractions.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace GuildArena.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // --- 1. CORE SERVICES (Logic) ---
        services.AddScoped<ICombatEngine, CombatEngine>();
        services.AddScoped<ITurnManagerService, TurnManagerService>();
        services.AddScoped<IEssenceService, EssenceService>();
        services.AddScoped<ITriggerProcessor, TriggerProcessor>();
        services.AddScoped<ITargetResolutionService, TargetResolutionService>();
        services.AddScoped<IStatusConditionService, StatusConditionService>();
        services.AddScoped<IActionQueue, ActionQueue>();

        // --- 2. CALCULATION SERVICES ---
        services.AddScoped<IDamageResolutionService, DamageResolutionService>();
        services.AddScoped<ICostCalculationService, CostCalculationService>();
        services.AddScoped<ICooldownCalculationService, CooldownCalculationService>();
        services.AddScoped<IStatCalculationService, StatCalculationService>();
        services.AddScoped<IHitChanceService, HitChanceService>(); 

        // --- 3. FACTORIES ---
        services.AddScoped<ICombatantFactory, CombatantFactory>();

        // --- 4. UTILS ---
        services.AddSingleton<IRandomProvider, SystemRandomProvider>();

        // --- 5. HANDLERS (Strategy Pattern) ---
        services.AddScoped<IEffectHandler, DamageEffectHandler>();
        services.AddScoped<IEffectHandler, ApplyModifierHandler>();
        services.AddScoped<IEffectHandler, ManipulateEssenceHandler>();
        

        return services;
    }
    
}