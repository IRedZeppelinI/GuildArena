using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions;

// TODO: Atualmente retorna dados hardcoded  para desenvolvimento. 
// Precisa de ser ligado ao Repositório SQL quando a camada de persistência estiver pronta.
public interface IPlayerRosterService
{
    /// <summary>
    /// Retrieves the active team of heroes associated with the specified player.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <returns>
    /// A task containing a list of <see cref="HeroCharacter"/> entities representing the player's selected team,
    /// including their current persistence data such as Level, XP, and equipped Loadout.
    /// </returns>
    Task<List<HeroCharacter>> GetActiveTeamAsync(int playerId);
}