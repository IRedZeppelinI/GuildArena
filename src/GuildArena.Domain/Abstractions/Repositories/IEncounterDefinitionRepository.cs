using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

/// <summary>
/// Defines the contract for retrieving static encounter definitions (blueprints).
/// </summary>
public interface IEncounterDefinitionRepository
{
    /// <summary>
    /// Retrieves a specific encounter definition by its unique ID.
    /// </summary>
    /// <param name="encounterId">The unique identifier of the encounter.</param>
    /// <param name="definition">The found definition, or null.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetDefinition(string encounterId, out EncounterDefinition definition);

    /// <summary>
    /// Retrieves all loaded encounter definitions.
    /// </summary>
    IReadOnlyDictionary<string, EncounterDefinition> GetAllDefinitions();
}