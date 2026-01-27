using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;

namespace GuildArena.Infrastructure.Persistence.Repositories;

public class PlayerRepository : IPlayerRepository
{
    public Task<List<Hero>> GetHeroesAsync(int playerId, List<int> heroIds)
    {
        // STUB DB

        var result = new List<Hero>();

        foreach (var id in heroIds)
        {
            // _context.Heroes.Where(h => ids.Contains(h.Id) && h.OwnerId == playerId)

            //A fornecer herois harcoded ate criar infrastrutura sql
            // TODO: Mudar base de herois para GuildRepository, Player será para questoes de conta de user
            //TODO: Remover hardcoded de DEBUG
            // ID 101 -> Garret
            // ID 102 -> Korg
            // ID 103 -> Elysia
            // ID 104 -> Vex
            // ID 105 -> Nyx
            // Outros -> Garret (Fallback)
            string defId = id switch
            {
                //101 => "HERO_GARRET",
                //102 => "HERO_KORG",
                103 => "HERO_ELYSIA",
                104 => "HERO_VEX",
                105 => "HERO_NYX",
                _ => "HERO_GARRET"
            };

            result.Add(new Hero
            {
                Id = id,
                GuildId = playerId,
                CharacterDefinitionID = defId,
                CurrentLevel = 1,
                CurrentXP = 0
            });
        }

        return Task.FromResult(result);
    }
}