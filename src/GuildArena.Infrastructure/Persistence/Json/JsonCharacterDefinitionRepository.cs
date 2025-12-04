using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

public class JsonCharacterDefinitionRepository : ICharacterDefinitionRepository
{
    private readonly Dictionary<string, CharacterDefinition> _definitions;
    private readonly ILogger<JsonCharacterDefinitionRepository> _logger;
    private readonly GameDataOptions _options;

    public JsonCharacterDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonCharacterDefinitionRepository> logger)
    {
        _logger = logger;
        _options = options.Value;
        _definitions = new Dictionary<string, CharacterDefinition>();

        LoadDefinitions();
    }

    public bool TryGetDefinition(string characterId, out CharacterDefinition definition)
    {
        return _definitions.TryGetValue(characterId, out definition!);
    }

    public IReadOnlyDictionary<string, CharacterDefinition> GetAllDefinitions()
    {
        return _definitions;
    }

    private void LoadDefinitions()
    {
        var filePath = Path.Combine(_options.AbsoluteRootPath, _options.CharactersFile);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Characters file not found at: {filePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var serializerOptions = JsonOptionsFactory.Create();
            var list = JsonSerializer.Deserialize<List<CharacterDefinition>>(jsonContent, serializerOptions);

            if (list == null) throw new Exception("JSON deserialization returned null.");

            foreach (var def in list)
            {
                if (_definitions.ContainsKey(def.Id))
                {
                    _logger.LogError("Duplicate Character ID: {Id}", def.Id);
                    continue;
                }
                _definitions[def.Id] = def;
            }

            _logger.LogInformation("Loaded {Count} characters from {Path}", _definitions.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load characters.");
            throw;
        }
    }
}