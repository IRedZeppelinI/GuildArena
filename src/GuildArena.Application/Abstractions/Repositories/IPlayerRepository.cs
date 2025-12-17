using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions.Repositories;

// TODO: Esta interface abstrai o acesso à base de dados (SQL).
// No futuro, a implementação usará Entity Framework Core.
public interface IPlayerRepository
{
    // <summary>
    /// Retrieves specific hero instances owned by a player.
    /// Validates ownership implicitly (only returns if OwnerId matches).
    /// </summary>
    /// <param name="playerId">The owner ID.</param>
    /// <param name="heroIds">The list of hero instance IDs to fetch.</param>
    Task<List<HeroCharacter>> GetHeroesAsync(int playerId, List<int> heroIds);

    // Futuramente terás aqui métodos como:
    // Task<PlayerProfile?> GetProfileAsync(int playerId);
    // Task SaveProgressAsync(int playerId, ...);
}