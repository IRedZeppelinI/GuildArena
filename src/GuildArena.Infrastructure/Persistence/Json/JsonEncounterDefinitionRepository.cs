using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

public class JsonEncounterDefinitionRepository : IEncounterDefinitionRepository
{
    private readonly Dictionary<string, EncounterDefinition> _definitions;
    private readonly ILogger<JsonEncounterDefinitionRepository> _logger;
    private readonly GameDataOptions _options;

    public JsonEncounterDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonEncounterDefinitionRepository> logger)
    {
        _logger = logger;
        _options = options.Value;
        _definitions = new Dictionary<string, EncounterDefinition>();

        LoadDefinitions();
    }

    public bool TryGetDefinition(string encounterId, out EncounterDefinition definition)
    {
        return _definitions.TryGetValue(encounterId, out definition!);
    }

    public IReadOnlyDictionary<string, EncounterDefinition> GetAllDefinitions()
    {
        return _definitions;
    }

    private void LoadDefinitions()
    {
        var folderPath = Path.Combine(_options.AbsoluteRootPath, _options.EncountersFolder);

        if (!Directory.Exists(folderPath))
        {            
            throw new DirectoryNotFoundException($"Encounters directory not found at: {folderPath}");
        }

        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        var options = JsonOptionsFactory.Create();

        foreach (var filePath in files)
        {
            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var list = JsonSerializer.Deserialize<List<EncounterDefinition>>(jsonContent, options);

                if (list == null) continue;

                foreach (var def in list)
                {
                    if (_definitions.ContainsKey(def.Id))
                    {
                        _logger.LogError("Duplicate Encounter ID: {Id} in file {File}", def.Id, Path.GetFileName(filePath));
                        continue;
                    }
                    _definitions[def.Id] = def;
                }
                _logger.LogInformation("Loaded {Count} encounters from {File}", list.Count, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to load encounters from {File}", Path.GetFileName(filePath));
                throw;
            }
        }
    }
}