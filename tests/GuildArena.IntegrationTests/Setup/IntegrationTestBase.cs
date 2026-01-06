using GuildArena.Application;
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Core;
using GuildArena.Domain.Abstractions.Repositories; // Necessário para as interfaces
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence.Json; // Necessário para os repos JSON
using GuildArena.IntegrationTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GuildArena.IntegrationTests.Setup;

public abstract class IntegrationTestBase
{
    protected readonly IServiceProvider Provider;

    protected IntegrationTestBase()
    {
        var services = new ServiceCollection();

        // 1. CONFIGURAR DADOS
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        services.Configure<GameDataOptions>(opts =>
        {
            opts.AbsoluteRootPath = dataPath;
            opts.AbilitiesFolder = "Abilities";
            opts.ModifiersFolder = "Modifiers";
            opts.RacesFile = "races.json";
            opts.CharactersFolder = "Characters";
            opts.EncountersFolder = "Encounters";
        });

        // 2. CONFIGURAR LOGGING
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // 3. REGISTAR SERVIÇOS DO CORE E APPLICATION
        services.AddCoreServices();
        services.AddApplicationServices();

        // 4. REGISTAR INFRAESTRUTURA 

        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();
        services.AddSingleton<IAbilityDefinitionRepository, JsonAbilityDefinitionRepository>();
        services.AddSingleton<IRaceDefinitionRepository, JsonRaceDefinitionRepository>();
        services.AddSingleton<ICharacterDefinitionRepository, JsonCharacterDefinitionRepository>();
        services.AddSingleton<IEncounterDefinitionRepository, JsonEncounterDefinitionRepository>();
        
        var playerRepoMock = Substitute.For<IPlayerRepository>();
        services.AddSingleton(playerRepoMock);

        // 5. REGISTAR O FAKE REDIS        
        services.AddSingleton<ICombatStateRepository, InMemoryCombatStateRepository>();

        // 6. SIMULAR UTILIZADOR (Player 1)
        //services.AddScoped<ICurrentUserService>(sp =>
        //{
        //    var mock = Substitute.For<ICurrentUserService>();
        //    mock.UserId.Returns(1);
        //    return mock;
        //});
        //Como singleton para alterar durante testes
        var userMock = Substitute.For<ICurrentUserService>();
        userMock.UserId.Returns(1); // Default Player 1        
        services.AddSingleton(userMock);


        // 7. CONSTRUIR
        Provider = services.BuildServiceProvider();
    }

    protected T GetService<T>() where T : notnull
    {
        return Provider.GetRequiredService<T>();
    }
}