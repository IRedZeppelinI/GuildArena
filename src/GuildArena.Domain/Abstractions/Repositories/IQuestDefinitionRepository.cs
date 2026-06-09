using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

/// <summary>
/// Defines the contract for retrieving static quest definitions (blueprints).
/// </summary>
public interface IQuestDefinitionRepository
{
    /// <summary>
    /// Retrieves a specific quest definition by its unique ID.
    /// </summary>
    /// <param name="id">The unique identifier of the quest.</param>
    /// <param name="definition">The found definition, or null.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetDefinition(string id, out QuestDefinition definition);

    /// <summary>
    /// Retrieves all loaded quest definitions.
    /// </summary>
    IReadOnlyDictionary<string, QuestDefinition> GetAllDefinitions();
}