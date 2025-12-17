using GuildArena.Api.Services;
using GuildArena.Application;
using GuildArena.Application.Abstractions;
using GuildArena.Core;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Infrastructure;
using GuildArena.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

// ler e deserializar GameDataOptions do appsetings
builder.Services.Configure<GameDataOptions>(builder.Configuration.GetSection(GameDataOptions.SectionName));

// Pós-configuração injetar o caminho absoluto
builder.Services.PostConfigure<GameDataOptions>(options =>
{
    // construir o absolutePath
    options.AbsoluteRootPath = Path.Combine(
        builder.Environment.ContentRootPath,
        options.RootFolder
    );
});

// Add services to the container.
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCoreServices();

//API services 
builder.Services.AddHttpContextAccessor(); 
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();


// Load defRepos
using (var scope = app.Services.CreateScope())
{
    try
    {
        var modifierRepo = scope.ServiceProvider.GetRequiredService<IModifierDefinitionRepository>();
        var abilityRepo = scope.ServiceProvider.GetRequiredService<IAbilityDefinitionRepository>();
        var raceRepo = scope.ServiceProvider.GetRequiredService<IRaceDefinitionRepository>(); 
        var charRepo = scope.ServiceProvider.GetRequiredService<ICharacterDefinitionRepository>(); 
        // GetRequiredService corre e carrega os JSONs para falhar no arranque se houver erro        
    }
    catch (Exception ex)
    {        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "CRITICAL: Failed to load game data definitions.");
        throw; // Impedir o arranque
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
