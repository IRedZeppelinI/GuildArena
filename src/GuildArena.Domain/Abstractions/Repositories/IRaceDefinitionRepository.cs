using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

public interface IRaceDefinitionRepository
{
    bool TryGetDefinition(string raceId, out RaceDefinition definition);
    IReadOnlyDictionary<string, RaceDefinition> GetAllDefinitions();
}