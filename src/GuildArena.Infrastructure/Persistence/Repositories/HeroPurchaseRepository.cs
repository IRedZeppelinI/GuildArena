using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GuildArena.Infrastructure.Persistence.Repositories;

public class HeroPurchaseRepository : IHeroPurchaseRepository
{
    private readonly GuildArenaDbContext _context;

    public HeroPurchaseRepository(GuildArenaDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(HeroPurchase purchase, CancellationToken cancellationToken = default)
    {
        await _context.HeroPurchases.AddAsync(purchase, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    //removido temporariamente. GuildRooster já mostra os desbloqueados
    //public async Task<bool> IsHeroUnlockedByGuildAsync(int guildId, string characterDefinitionId, CancellationToken cancellationToken = default)
    //{
    //    return await _context.HeroPurchases
    //        .AnyAsync(p => p.GuildId == guildId && p.CharacterDefinitionId == characterDefinitionId, cancellationToken);
    //}
}