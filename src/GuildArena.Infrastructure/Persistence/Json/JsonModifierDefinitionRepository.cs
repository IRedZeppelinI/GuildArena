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
        // POR ENQUANTO: Vamos inicializar com dados vazios ou hardcoded para a app não rebentar.
        // (Futuro: Ler de um ficheiro 'modifiers.json' real usando System.IO)

        _definitions = new Dictionary<string, ModifierDefinition>();

        // Exemplo de dados "dummy" para testes (pode remover depois):
        // var dummyMod = new ModifierDefinition { Id = "MOD_TEST", Name = "Test Mod", ... };
        // _definitions.Add(dummyMod.Id, dummyMod);
    }

    public IReadOnlyDictionary<string, ModifierDefinition> GetAllDefinitions()
    {
        return _definitions;
    }
}