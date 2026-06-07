// FILE: tests/GuildArena.IntegrationTests/Setup/IntegrationTestBase.cs

using GuildArena.Application;
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Notifications;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Core;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Infrastructure.Options;
using GuildArena.Infrastructure.Persistence.Json;
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
            opts.DungeonsFolder = "Dungeons"; // <--- ADICIONAR AQUI
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

        // 4. REGISTAR INFRAESTRUTURA JSON (Real)
        services.AddSingleton<IModifierDefinitionRepository, JsonModifierDefinitionRepository>();
        services.AddSingleton<IAbilityDefinitionRepository, JsonAbilityDefinitionRepository>();
        services.AddSingleton<IRaceDefinitionRepository, JsonRaceDefinitionRepository>();
        services.AddSingleton<ICharacterDefinitionRepository, JsonCharacterDefinitionRepository>();
        services.AddSingleton<IEncounterDefinitionRepository, JsonEncounterDefinitionRepository>();

        // <--- ADICIONAR O REPOSITÓRIO DAS DUNGEONS AQUI --->
        services.AddSingleton<IDungeonDefinitionRepository, JsonDungeonDefinitionRepository>();

        // Repositórios da Base de Dados (Mockados)
        var guildRepoMock = Substitute.For<IGuildRepository>();
        services.AddSingleton(guildRepoMock);

        var matchRepoMock = Substitute.For<IMatchRepository>();
        services.AddSingleton(matchRepoMock);

        var dungeonRunRepoMock = Substitute.For<IDungeonRunRepository>();
        services.AddSingleton(dungeonRunRepoMock);

        // 5. REGISTAR O FAKE REDIS        
        services.AddSingleton<ICombatStateRepository, InMemoryCombatStateRepository>();

        // 6. SIMULAR UTILIZADOR (Player 1)
        var userMock = Substitute.For<ICurrentUserService>();
        userMock.UserId.Returns("user-123");
        services.AddSingleton(userMock);

        // 7. SIMULAR NOTIFICADOR 
        var notifierMock = Substitute.For<ICombatNotifier>();
        services.AddSingleton(notifierMock);

        // 8. CONSTRUIR
        Provider = services.BuildServiceProvider();
    }

    protected T GetService<T>() where T : notnull
    {
        return Provider.GetRequiredService<T>();
    }
}