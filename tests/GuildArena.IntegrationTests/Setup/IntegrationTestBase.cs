using GuildArena.Application; 
using GuildArena.Application.Abstractions;
using GuildArena.Core;       
using GuildArena.Infrastructure; 
using GuildArena.Infrastructure.Options;
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

        // 3. REGISTAR SERVIÇOS REAIS
        // Estes métodos de extensão vêm dos namespaces GuildArena.Core e GuildArena.Application
        services.AddCoreServices();
        services.AddApplicationServices();
        services.AddInfrastructureServices(null!);

        // 4. SUBSTITUIÇÃO (MOCKS & FAKES)

        // Remover Redis real
        var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICombatStateRepository));
        if (redisDescriptor != null) services.Remove(redisDescriptor);

        // Adicionar Fake em memória
        services.AddSingleton<ICombatStateRepository, InMemoryCombatStateRepository>();

        // Simular User
        services.AddScoped<ICurrentUserService>(sp =>
        {
            var mock = Substitute.For<ICurrentUserService>();
            mock.UserId.Returns(1);
            return mock;
        });


        // 5. CONSTRUIR
        Provider = services.BuildServiceProvider();
    }

    protected T GetService<T>() where T : notnull
    {
        return Provider.GetRequiredService<T>();
    }
}