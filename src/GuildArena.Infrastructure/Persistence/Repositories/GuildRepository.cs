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
            .FirstOrDefaultAsync(g => g.ApplicationUserId == applicationUserId);
    }

    public async Task<List<Hero>> GetHeroesAsync(int guildId, List<int> heroIds)
    {
        return await _dbContext.Heroes
            .AsNoTracking()
            .Where(hero => hero.GuildId == guildId && heroIds.Contains(hero.Id))
            .ToListAsync();
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
}