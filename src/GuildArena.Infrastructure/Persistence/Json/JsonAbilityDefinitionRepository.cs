using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

public class JsonAbilityDefinitionRepository : IAbilityDefinitionRepository
{
    private readonly Dictionary<string, AbilityDefinition> _definitions;
    private readonly ILogger<JsonAbilityDefinitionRepository> _logger;
    private readonly GameDataOptions _options;

    public JsonAbilityDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonAbilityDefinitionRepository> logger)
    {
        _logger = logger;
        _options = options.Value;
        _definitions = new Dictionary<string, AbilityDefinition>();

        LoadDefinitions();
    }

    public bool TryGetDefinition(string abilityId, out AbilityDefinition definition)
    {
        return _definitions.TryGetValue(abilityId, out definition!);
    }

    public IReadOnlyDictionary<string, AbilityDefinition> GetAllDefinitions()
    {
        return _definitions;
    }

    private void LoadDefinitions()
    {
        // 1. Construir o caminho da pasta de Habilidades
        var abilitiesPath = Path.Combine(_options.AbsoluteRootPath, _options.AbilitiesFolder);

        if (!Directory.Exists(abilitiesPath))
        {
            _logger.LogError("Abilities directory not found at: {Path}", abilitiesPath);
            throw new DirectoryNotFoundException($"Critical data missing: Abilities folder at {abilitiesPath}");
        }

        // 2. Obter todos os ficheiros JSON na pasta
        var files = Directory.GetFiles(abilitiesPath, "*.json", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            _logger.LogWarning("No ability files found in {Path}", abilitiesPath);
        }

        var options = JsonOptionsFactory.Create();

        // 3. Iterar e carregar cada ficheiro
        foreach (var filePath in files)
        {
            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var list = JsonSerializer.Deserialize<List<AbilityDefinition>>(jsonContent, options);

                if (list == null) continue;

                foreach (var def in list)
                {
                    if (_definitions.ContainsKey(def.Id))
                    {
                        _logger.LogCritical("Duplicate Ability ID detected: {Id} in file {File}", def.Id, Path.GetFileName(filePath));
                        throw new Exception($"Duplicate Ability ID: {def.Id}");
                    }

                    _definitions[def.Id] = def;
                }

                _logger.LogInformation("Loaded {Count} abilities from {File}", list.Count, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error loading abilities from {File}", Path.GetFileName(filePath));
                throw; // Fail Fast
            }
        }

        _logger.LogInformation("Total Abilities Loaded: {Total}", _definitions.Count);
    }
}