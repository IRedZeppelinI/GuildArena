using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GuildArena.Infrastructure.Persistence.Repositories;

public class GuildRepository : IGuildRepository
{
    private readonly GuildArenaDbContext _dbContext;

    public GuildRepository(GuildArenaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guild?> GetGuildByUserIdAsync(string applicationUserId)
    {
        return await _dbContext.Guilds
            .AsNoTracking()
            .Include(g => g.DungeonRecords)
            .FirstOrDefaultAsync(g => g.ApplicationUserId == applicationUserId);
    }

    public async Task<List<Hero>> GetHeroesAsync(int guildId, List<int> heroIds)
    {
        return await _dbContext.Heroes
            .AsNoTracking()
            .Where(hero => hero.GuildId == guildId && heroIds.Contains(hero.Id))
            .ToListAsync();
    }

    public async Task<List<Hero>> GetAllHeroesAsync(int guildId)
    {
        return await _dbContext.Heroes
            .AsNoTracking()
            .Where(hero => hero.GuildId == guildId)
            .ToListAsync();
    }

    public async Task CreateWithStarterPackAsync(string applicationUserId, string guildName)
    {
        var guild = new Guild
        {
            ApplicationUserId = applicationUserId,
            Name = guildName,
            Gold = 500, // Starter Gold
            Wins = 0,
            Losses = 0,
            Heroes = new List<Hero>
            {
                new Hero { CharacterDefinitionId = "HERO_GARRET", CurrentLevel = 1, CurrentXP = 0 },
                new Hero { CharacterDefinitionId = "HERO_KORG", CurrentLevel = 1, CurrentXP = 0 },
                new Hero { CharacterDefinitionId = "HERO_ELYSIA", CurrentLevel = 1, CurrentXP = 0 }
            }
        };

        await _dbContext.Guilds.AddAsync(guild);
        await _dbContext.SaveChangesAsync();
    }

    public async Task CreateGuildAsync(Guild guild)
    {
        await _dbContext.Guilds.AddAsync(guild);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateGuildAsync(Guild guild)
    {
        _dbContext.Guilds.Update(guild);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Guild?> GetGuildWithHistoryAsync(string applicationUserId)
    {
        return await _dbContext.Guilds
            
            .Include(g => g.Heroes)
            .Include(g => g.MatchHistory)
                .ThenInclude(mh => mh.HeroesUsed) 
            .FirstOrDefaultAsync(g => g.ApplicationUserId == applicationUserId);
    }

}