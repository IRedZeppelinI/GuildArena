using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

/// <summary>
/// Loads all dungeon definitions from JSON files located in the configured Dungeons folder.
/// The data is cached as a singleton for the application lifetime.
/// </summary>
public class JsonDungeonDefinitionRepository : IDungeonDefinitionRepository
{
    private readonly IReadOnlyDictionary<string, DungeonDefinition> _definitions;
    private readonly ILogger<JsonDungeonDefinitionRepository> _logger;

    public JsonDungeonDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonDungeonDefinitionRepository> logger)
    {
        _logger = logger;
        var opts = options.Value;
        string folderPath = Path.Combine(opts.AbsoluteRootPath, opts.DungeonsFolder);

        var dict = new Dictionary<string, DungeonDefinition>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("Dungeons folder not found: {Path}", folderPath);
            _definitions = dict;
            return;
        }

        var files = Directory.GetFiles(folderPath, "*.json");
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var file in files)
        {
            string content = File.ReadAllText(file);
            var definitions = JsonSerializer.Deserialize<List<DungeonDefinition>?>(content, serializerOptions);
            if (definitions != null)
            {
                foreach (var def in definitions)
                {
                    if (!dict.TryAdd(def.Id, def))
                    {
                        _logger.LogWarning("Duplicate dungeon ID {Id} in file {File}", def.Id, file);
                    }
                }
            }
        }

        _definitions = dict;
        _logger.LogInformation("Loaded {Count} dungeon definitions.", _definitions.Count);
    }

    public bool TryGetDefinition(string dungeonId, out DungeonDefinition definition)
    {
        return _definitions.TryGetValue(dungeonId, out definition!);
    }

    public IReadOnlyDictionary<string, DungeonDefinition> GetAllDefinitions()
    {
        return _definitions;
    }
}