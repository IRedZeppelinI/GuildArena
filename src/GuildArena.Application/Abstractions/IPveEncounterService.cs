using GuildArena.Domain.Entities;

namespace GuildArena.Application.Abstractions;

// TODO: Atualmente hardcoded para retornar Bandidos. 
// A implementação futura deve ler de Definições de Encontros estáticas (JSON) ou da Base de Dados.
public interface IPveEncounterService
{
    /// <summary>
    /// Retrieves the enemy composition for a specific PvE encounter.
    /// </summary>
    /// <param name="encounterId">The unique identifier of the encounter or dungeon stage.</param>
    /// <returns>
    /// A task containing a list of <see cref="HeroCharacter"/> entities representing the enemies to be spawned.
    /// </returns>
    Task<List<HeroCharacter>> GetEncounterMobsAsync(string encounterId);
}