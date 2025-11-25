using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

/// <summary>
/// Defines the contract for retrieving ability definitions (blueprints).
/// </summary>
public interface IAbilityDefinitionRepository
{
    /// <summary>
    /// Retrieves a specific ability definition by its unique ID.
    /// </summary>
    /// <param name="abilityId">The unique identifier of the ability.</param>
    /// <param name="definition">The found definition, or null.</param>
    /// <returns>True if found, false otherwise.</returns>
    bool TryGetDefinition(string abilityId, out AbilityDefinition definition);

    //  GetAllDefinitions() tal como no ModifierRepo
    //  TryGet para lookups pontuais.
    IReadOnlyDictionary<string, AbilityDefinition> GetAllDefinitions();
}