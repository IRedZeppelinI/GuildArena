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

        return services;
    }
}