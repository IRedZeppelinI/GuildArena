using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence.Context;
using GuildArena.Infrastructure.Persistence.Json;
using GuildArena.Infrastructure.Persistence.Redis;
using GuildArena.Infrastructure.Persistence.Repositories;
using GuildArena.Infrastructure.Services;
using GuildArena.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GuildArena.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Handles the registration of infrastructure services, such as database contexts, 
    /// Redis connections, and data repositories.
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
        //services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        //{
        //    options.User.RequireUniqueEmail = true;
        //    // Regras relaxadas para desenvolvimento
        //    options.Password.RequireDigit = false;
        //    options.Password.RequiredLength = 4;
        //    options.Password.RequireNonAlphanumeric = false;
        //    options.Password.RequireUppercase = false;
        //    options.Password.RequireLowercase = false;
        //})
        //    .AddEntityFrameworkStores<GuildArenaDbContext>()
        //    .AddDefaultTokenProviders();


        // ==========================================
        //  Email Services
        // ==========================================

        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));

        services.AddHttpClient("BrevoClient", (sp, client) =>
        {
            var brevoOptions = sp.GetRequiredService<IOptions<BrevoOptions>>().Value;
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");

            if (!string.IsNullOrEmpty(brevoOptions.ApiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", brevoOptions.ApiKey);
            }
        });

        //services.AddScoped<IEmailDispatcher, BrevoEmailDispatcher>();
        //services.AddScoped<IEmailSender<ApplicationUser>, SpaIdentityEmailSender>();
        services.AddTransient<IEmailDispatcher, BrevoEmailDispatcher>();
        services.AddTransient<IEmailSender<ApplicationUser>, SpaIdentityEmailSender>();


        // Repositórios

        // ==========================================
        //  Redis
        // ==========================================
        services.AddScoped<ICombatStateRepository, RedisCombatStateRepository>();

        // ==========================================
        //  Jsons
        // ==========================================
        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();
        services.AddSingleton<IAbilityDefinitionRepository, JsonAbilityDefinitionRepository>();
        services.AddSingleton<IRaceDefinitionRepository, JsonRaceDefinitionRepository>();
        services.AddSingleton<ICharacterDefinitionRepository, JsonCharacterDefinitionRepository>();
        services.AddSingleton<IEncounterDefinitionRepository, JsonEncounterDefinitionRepository>();
        services.AddSingleton<IDungeonDefinitionRepository, JsonDungeonDefinitionRepository>();
        services.AddSingleton<IQuestDefinitionRepository, JsonQuestDefinitionRepository>();

        // ==========================================
        //  SQL
        // ==========================================
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IHeroPurchaseRepository, HeroPurchaseRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IDungeonRunRepository, DungeonRunRepository>();
        services.AddScoped<INewsRepository, NewsRepository>();

        // ==========================================
        //  Azure
        // ==========================================
        services.AddScoped<IStorageService, AzureBlobStorageService>();

        return services;
    }
}