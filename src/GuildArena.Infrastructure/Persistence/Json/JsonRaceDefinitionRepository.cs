using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

public class JsonRaceDefinitionRepository : IRaceDefinitionRepository
{
    private readonly Dictionary<string, RaceDefinition> _definitions;
    private readonly ILogger<JsonRaceDefinitionRepository> _logger;
    private readonly GameDataOptions _options;

    public JsonRaceDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonRaceDefinitionRepository> logger)
    {
        _logger = logger;
        _options = options.Value;
        _definitions = new Dictionary<string, RaceDefinition>();

        LoadDefinitions();
    }

    public IReadOnlyDictionary<string, RaceDefinition> GetAllDefinitions()
    {
        return _definitions;
    }

    public bool TryGetDefinition(string raceId, out RaceDefinition definition)
    {
        return _definitions.TryGetValue(raceId, out definition!);
    }

    private void LoadDefinitions()
    {
        var filePath = Path.Combine(_options.AbsoluteRootPath, _options.RacesFile);

        if (!File.Exists(filePath))
        {            
            throw new FileNotFoundException($"Races file not found at: {filePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var serializerOptions = JsonOptionsFactory.Create();
            var list = JsonSerializer.Deserialize<List<RaceDefinition>>(jsonContent, serializerOptions);

            if (list == null) throw new Exception("JSON deserialization returned null.");

            foreach (var def in list)
            {
                if (_definitions.ContainsKey(def.Id))
                {
                    _logger.LogError("Duplicate Race ID: {Id}", def.Id);
                    continue;
                }
                _definitions[def.Id] = def;
            }

            _logger.LogInformation("Loaded {Count} races from {Path}", _definitions.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load races.");
            throw;
        }
    }
}