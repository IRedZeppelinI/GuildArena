using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Entities;

namespace GuildArena.Infrastructure.Persistence.Repositories;

public class PlayerRepository : IPlayerRepository
{
    public Task<List<HeroCharacter>> GetHeroesAsync(int playerId, List<int> heroIds)
    {
        // STUB: Simula uma base de dados onde o jogador tem alguns heróis.
        // Vamos fingir que o jogador tem sempre os heróis que pede, 
        // desde que os IDs sejam "válidos" para efeitos de teste.

        var result = new List<HeroCharacter>();

        foreach (var id in heroIds)
        {
            // Simulação: Cria o herói on-the-fly baseado no ID pedido.
            // Num cenário real, faria: _context.Heroes.Where(h => ids.Contains(h.Id) && h.OwnerId == playerId)

            result.Add(new HeroCharacter
            {
                Id = id,
                GuildId = playerId,
                CharacterDefinitionID = "HERO_GARRET", // Hardcoded para o teste funcionar
                CurrentLevel = 1,
                CurrentXP = 0
            });
        }

        return Task.FromResult(result);
    }
}