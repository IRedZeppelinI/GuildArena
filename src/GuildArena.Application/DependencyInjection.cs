using GuildArena.Application.Abstractions;
using GuildArena.Application.Combat.AI;
using GuildArena.Application.Combat.AI.BackgroundServices;
using GuildArena.Application.Combat.AI.Behaviors;
using GuildArena.Application.Combat.Resolution;
using GuildArena.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace GuildArena.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Regista o MediatR e varre a Assembly atual à procura de Handlers (Commands/Queries)
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // (Se no futuro usarmos AutoMapper ou Validators, registamo-los aqui também)
        services.AddScoped<IAiBehavior, RandomAiBehavior>();
        services.AddScoped<IAiTurnOrchestrator, AiTurnOrchestrator>();


        services.AddSingleton<IAiTurnQueue, AiTurnQueue>();
        services.AddHostedService<AiTurnWorker>();

        
        services.AddScoped<IGuildService, GuildService>();
        services.AddScoped<IEncounterService, EncounterService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IEffectTooltipService, EffectTooltipService>();
        services.AddScoped<IHeroUnlockEvaluator, HeroUnlockEvaluator>();
        services.AddScoped<IQuestService, QuestService>();

        // Guild progression
        services.AddScoped<IGuildProgressionService, GuildProgressionService>();

        //Combat Resolution
        services.AddScoped<IMatchTypeResolver, EncounterMatchResolver>();
        services.AddScoped<IMatchTypeResolver, DungeonMatchResolver>();
        services.AddScoped<ICombatResolutionService, CombatResolutionService>();

        return services;
    }
}