using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

/// <summary>
/// Defines the contract for retrieving static dungeon definitions (blueprints).
/// </summary>
public interface IDungeonDefinitionRepository
{
    /// <summary>
    /// Retrieves a specific dungeon definition by its unique ID.
    /// </summary>
    /// <param name="dungeonId">The unique identifier of the dungeon.</param>
    /// <param name="definition">The found definition, or null.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetDefinition(string dungeonId, out DungeonDefinition definition);

    /// <summary>
    /// Retrieves all loaded dungeon definitions.
    /// </summary>
    IReadOnlyDictionary<string, DungeonDefinition> GetAllDefinitions();
}