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
        //  PASTA Characters
        var folderPath = Path.Combine(_options.AbsoluteRootPath, _options.CharactersFolder);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Characters directory not found at: {folderPath}");
        }

        // 2. Ler todos os ficheiros (heroes.json, mobs.json, bosses.json...)
        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        var options = JsonOptionsFactory.Create();

        foreach (var filePath in files)
        {
            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var list = JsonSerializer.Deserialize<List<CharacterDefinition>>(jsonContent, options);

                if (list == null) continue;

                foreach (var def in list)
                {
                    if (_definitions.ContainsKey(def.Id))
                    {
                        _logger.LogError("Duplicate Character ID: {Id} in file {File}", def.Id, Path.GetFileName(filePath));
                        throw new Exception($"Duplicate Character ID: {def.Id}");
                    }
                    _definitions[def.Id] = def;
                }

                _logger.LogInformation("Loaded {Count} characters from {File}", list.Count, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to load characters from {File}", Path.GetFileName(filePath));
                throw;
            }
        }
    }
}