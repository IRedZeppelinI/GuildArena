using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

public interface ICharacterDefinitionRepository
{
    /// <summary>
    /// Retrieves a specific character definition by its unique ID.
    /// </summary>
    bool TryGetDefinition(string characterId, out CharacterDefinition definition);

    /// <summary>
    /// Retrieves all loaded character definitions.
    /// </summary>
    IReadOnlyDictionary<string, CharacterDefinition> GetAllDefinitions();
}