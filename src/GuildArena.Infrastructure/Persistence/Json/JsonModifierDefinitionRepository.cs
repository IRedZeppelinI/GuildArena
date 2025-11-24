using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using System.Text.Json;

namespace GuildArena.Infrastructure.Persistence.Json;

/// <summary>
/// Reads modifier definitions from a JSON file and caches them in memory.
/// </summary>
public class JsonModifierDefinitionRepository : IModifierDefinitionRepository
{
    private readonly Dictionary<string, ModifierDefinition> _definitions;

    public JsonModifierDefinitionRepository()
    {
        
        // TODO modifiers.json

        _definitions = new Dictionary<string, ModifierDefinition>();
        
    }

    public IReadOnlyDictionary<string, ModifierDefinition> GetAllDefinitions()
    {
        return _definitions;
    }
}