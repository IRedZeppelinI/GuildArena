using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuildArena.Infrastructure.Persistence;

/// <summary>
/// Responsible for populating the database with essential initial data (Roles) 
/// and development mock data (Test Users, Guilds, and Heroes).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        GuildArenaDbContext dbContext,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        bool isDevelopment)
    {
        logger.LogInformation("Starting database seeding process...");

        // 1. Always seed essential application roles
        await SeedRolesAsync(roleManager, logger);

        // 2. Only seed test accounts and full hero rosters in Development environment
        if (isDevelopment)
        {
            logger.LogInformation("Development environment detected. Seeding mock data...");
            await SeedDevelopmentDataAsync(dbContext, userManager, logger);
        }

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

    private static async Task SeedDevelopmentDataAsync(
        GuildArenaDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        const string devEmail = "dev@guildarena.com";
        const string devPass = "Password123!";

        var devUser = await userManager.FindByEmailAsync(devEmail);

        if (devUser == null)
        {
            devUser = new ApplicationUser
            {
                UserName = devEmail,
                Email = devEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(devUser, devPass);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(devUser, "Admin");
                await userManager.AddToRoleAsync(devUser, "Player");
                logger.LogInformation("Development user '{Email}' created successfully.", devEmail);
            }
            else
            {
                logger.LogError("Failed to create development user. Errors: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        bool hasGuild = await dbContext.Guilds.AnyAsync(g => g.ApplicationUserId == devUser.Id);

        if (!hasGuild)
        {
            var guild = new Guild
            {
                ApplicationUserId = devUser.Id,
                Name = "Dev Team",
                Gold = 9999,
                Wins = 0,
                Losses = 0,
                Heroes = new List<Hero>
                {
                    new Hero { CharacterDefinitionId = "HERO_GARRET", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_KORG", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_ELYSIA", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_VEX", CurrentLevel = 1, CurrentXP = 0 },
                    new Hero { CharacterDefinitionId = "HERO_NYX", CurrentLevel = 1, CurrentXP = 0 }
                }
            };

            await dbContext.Guilds.AddAsync(guild);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Mock Guild 'Dev Team' with full hero roster created for development user.");
        }
    }
}