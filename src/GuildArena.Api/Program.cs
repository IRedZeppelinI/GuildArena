using GuildArena.Api;
using GuildArena.Api.Hubs;
using GuildArena.Api.Mappers;
using GuildArena.Api.Services;
using GuildArena.Api.Services.Notifications;
using GuildArena.Application;
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Core;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// --- INÍCIO CONFIGURAÇÃO CORS ---
var allowedOrigins = builder.Configuration.
    GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); 
    });
});


//// ler e deserializar GameDataOptions do appsetings
//builder.Services.Configure<GameDataOptions>(builder.Configuration.GetSection(GameDataOptions.SectionName));

//// Pós-configuração injetar o caminho absoluto
//builder.Services.PostConfigure<GameDataOptions>(options =>
//{
//    // construir o absolutePath
//    options.AbsoluteRootPath = Path.Combine(
//        builder.Environment.ContentRootPath,
//        options.RootFolder
//    );
//});

//// Add services to the container.
//builder.Services.AddInfrastructureServices(builder.Configuration);
//builder.Services.AddApplicationServices();
//builder.Services.AddCoreServices();

////API services 
//builder.Services.AddHttpContextAccessor(); 
//builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

//// SignalR
//builder.Services.AddSignalR();
//builder.Services.AddScoped<ICombatNotifier, SignalRCombatNotifier>();

//builder.Services.AddScoped<ICombatStateMapper, CombatStateMapper>();

//builder.Services.AddControllers();
//// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

////  Identity Endpoints 
//builder.Services.AddAuthorization();
//builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
//    .AddEntityFrameworkStores<GuildArena.Infrastructure.Persistence.Context.GuildArenaDbContext>();

//var app = builder.Build();


//// Load defRepos
//using (var scope = app.Services.CreateScope())
//{
//    try
//    {
//        var modifierRepo = scope.ServiceProvider.GetRequiredService<IModifierDefinitionRepository>();
//        var abilityRepo = scope.ServiceProvider.GetRequiredService<IAbilityDefinitionRepository>();
//        var raceRepo = scope.ServiceProvider.GetRequiredService<IRaceDefinitionRepository>(); 
//        var charRepo = scope.ServiceProvider.GetRequiredService<ICharacterDefinitionRepository>(); 
//        // GetRequiredService corre e carrega os JSONs para falhar no arranque se houver erro        
//    }
//    catch (Exception ex)
//    {        
//        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//        logger.LogCritical(ex, "CRITICAL: Failed to load game data definitions.");
//        throw; // Impedir o arranque
//    }
//}


//// --- Seed Database ---
//using (var scope = app.Services.CreateScope())
//{
//    var services = scope.ServiceProvider;
//    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
//    var logger = loggerFactory.CreateLogger("DatabaseSeeder");

//    try
//    {
//        var dbContext = services.GetRequiredService<GuildArenaDbContext>();
//        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
//        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

//        await DataSeeder.SeedAsync(
//            dbContext,
//            roleManager,
//            userManager,
//            logger,
//            app.Environment.IsDevelopment());
//    }
//    catch (Exception ex)
//    {
//        logger.LogCritical(ex, "An error occurred during database seeding.");
//    }
//}


//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();

//app.UseCors("AllowBlazorWasm");

//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllers();

//// Endpoints Login/Registo 
//app.MapIdentityApi<ApplicationUser>();

//app.MapHub<CombatHub>("/hubs/combat");

//app.Run();


// --- GameDataOptions Configuration ---
builder.Services.Configure<GameDataOptions>(
    builder.Configuration.GetSection(GameDataOptions.SectionName));

builder.Services.PostConfigure<GameDataOptions>(options =>
{
    options.AbsoluteRootPath = Path.Combine(
        builder.Environment.ContentRootPath,
        options.RootFolder
    );
});

// --- Architecture Layers Registration ---
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCoreServices();

// --- API & Web Security Registration ---
// All Identity, SignalR, and API-specific services are now encapsulated here
builder.Services.AddApiServices(builder.Configuration);

// --- Standard API Features ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Application Warmup (Load JSON Definitions) ---
using (var scope = app.Services.CreateScope())
{
    try
    {
        // Instantiating these singletons forces the JSON files to be loaded into memory
        _ = scope.ServiceProvider.GetRequiredService<IModifierDefinitionRepository>();
        _ = scope.ServiceProvider.GetRequiredService<IAbilityDefinitionRepository>();
        _ = scope.ServiceProvider.GetRequiredService<IRaceDefinitionRepository>();
        _ = scope.ServiceProvider.GetRequiredService<ICharacterDefinitionRepository>();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "CRITICAL: Failed to load game data definitions during application warmup.");
        throw; // Fail fast to prevent the app from running in a corrupted state
    }
}

// --- Database Seeding ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var loggerFactory = services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("DatabaseSeeder");

    try
    {
        var dbContext = services.GetRequiredService<GuildArenaDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await DataSeeder.SeedAsync(
            dbContext,
            roleManager,
            userManager,
            logger,
            app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "An error occurred during database seeding.");
    }
}

// --- HTTP Request Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazorWasm");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map Identity API Endpoints (Login, Register, Manage)
app.MapIdentityApi<ApplicationUser>();

// Map SignalR Hub
app.MapHub<CombatHub>("/hubs/combat");

app.Run();