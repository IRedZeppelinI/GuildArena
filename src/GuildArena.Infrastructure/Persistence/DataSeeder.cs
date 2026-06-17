using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // Adicionar este using

namespace GuildArena.Infrastructure.Persistence;

/// <summary>
/// Responsible for populating the database with essential initial data (Roles) 
/// and development mock data (Test Users, Guilds, and Heroes).
/// </summary>
public static class DataSeeder
{
    // Adicionada a IConfiguration aos parâmetros (terás de atualizar a chamada no Program.cs da API)
    public static async Task SeedAsync(
        GuildArenaDbContext dbContext,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        bool isDevelopment,
        IConfiguration configuration)
    {
        logger.LogInformation("Starting database seeding process...");

        await SeedRolesAsync(roleManager, logger);

        // Agora criamos o Admin baseado na configuração (Seguro para Produção)
        await SeedAdminUserAsync(dbContext, userManager, logger, isDevelopment, configuration);

        // Seed das 3 notícias de boas-vindas (Corre em Dev e Prod, mas só se a tabela estiver vazia)
        await SeedNewsAsync(dbContext, logger);

        logger.LogInformation("Database seeding process completed.");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        string[] roles = { "Admin", "Player" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Role '{Role}' created.", role);
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        GuildArenaDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        bool isDevelopment,
        IConfiguration config)
    {
        // Vai buscar à configuração (Variáveis de Ambiente em Prod, ou hardcoded em Dev)
        string adminEmail = config["AdminSetup:Email"] ?? (isDevelopment ? "dev@guildarena.com" : null);
        string adminPass = config["AdminSetup:Password"] ?? (isDevelopment ? "Password123!" : null);

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPass))
        {
            logger.LogInformation("No Admin setup configuration found. Skipping admin creation.");
            return;
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(adminUser, adminPass);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                await userManager.AddToRoleAsync(adminUser, "Player");
                logger.LogInformation("Admin user '{Email}' created successfully.", adminEmail);
            }
            else
            {
                logger.LogError("Failed to create Admin user. Errors: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        // Criar guilda default apenas para desenvolvimento
        if (isDevelopment && !await dbContext.Guilds.AnyAsync(g => g.ApplicationUserId == adminUser.Id))
        {
            var guild = new Guild
            {
                ApplicationUserId = adminUser.Id,
                Name = "Dev Team",
                Gold = 9999,
                Wins = 0,
                Losses = 0,
                Heroes = new List<Hero>
                {
                    new Hero { CharacterDefinitionId = "HERO_GARRET", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_KORG", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_ELYSIA", CurrentLevel = 1, CurrentXP = 0 }
                }
            };
            await dbContext.Guilds.AddAsync(guild);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Mock Guild created for development user.");
        }
    }

    private static async Task SeedNewsAsync(GuildArenaDbContext dbContext, ILogger logger)
    {
        if (await dbContext.NewsArticles.AnyAsync()) return; // Já existem notícias, ignora

        var news = new List<NewsArticle>
        {
            new NewsArticle {
                Title = "Welcome to GuildArena!",
                Summary = "The Arena opens its gates to a new generation of tacticians.",
                Content = "Welcome to the alpha version of GuildArena! Assemble your roster, manage your essence, and prepare for glory.\n\nMore features will be unlocked soon.",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                IsPublished = true
            },
            new NewsArticle {
                Title = "Patch Notes: The First Balance",
                Summary = "Adjustments to essence economy and Valdrin stats.",
                Content = "We have slightly tweaked the initial essence generation to ensure the player going second has a fair chance to react. Furthermore, Korg's base defense has been buffed.",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsPublished = true
            },
            new NewsArticle {
                Title = "Upcoming Feature: The Dungeons",
                Summary = "Prepare your best team. A new PvE mode is coming.",
                Content = "Dungeon mode is actively being tested! You'll need to draft a 3-hero team and survive multiple stages where HP carries over between fights. Will your healers be able to keep up?",
                CreatedAt = DateTime.UtcNow,
                IsPublished = true
            }
        };

        await dbContext.NewsArticles.AddRangeAsync(news);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded 3 initial news articles.");
    }
}