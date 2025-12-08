using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GuildArena.Infrastructure.Persistence.Json;

/// <summary>
/// Reads modifier definitions from a JSON file and caches them in memory.
/// </summary>
public class JsonModifierDefinitionRepository : IModifierDefinitionRepository
{
    private readonly Dictionary<string, ModifierDefinition> _definitions;
    private readonly ILogger<JsonModifierDefinitionRepository> _logger;
    private readonly GameDataOptions _options;

    // Injetamos IOptions<GameDataOptions> em vez de IConfiguration ou IWebHostEnvironment
    public JsonModifierDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonModifierDefinitionRepository> logger)
    {
        _logger = logger;
        _options = options.Value;
        _definitions = new Dictionary<string, ModifierDefinition>();

        LoadDefinitions(); // Corre no momento da injeção (ver Warmup no Program.cs)
    }

    public IReadOnlyDictionary<string, ModifierDefinition> GetAllDefinitions()
    {
        return _definitions;
    }

    private void LoadDefinitions()
    {        
        var modifiersPath = Path.Combine(_options.AbsoluteRootPath, _options.ModifiersFolder);

        if (!Directory.Exists(modifiersPath))
        {
            throw new DirectoryNotFoundException($"Modifiers directory not found at: {modifiersPath}");
        }

        var files = Directory.GetFiles(modifiersPath, "*.json", SearchOption.TopDirectoryOnly);
        var options = JsonOptionsFactory.Create();

        foreach (var filePath in files)
        {
            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var list = JsonSerializer.Deserialize<List<ModifierDefinition>>(jsonContent, options);

                if (list == null) continue;

                foreach (var def in list)
                {
                    if (_definitions.ContainsKey(def.Id))
                    {
                        _logger.LogError("Duplicate Modifier ID: {Id} in {File}", def.Id, Path.GetFileName(filePath));
                        // Fail Fast para duplicados também
                        throw new Exception($"Duplicate Modifier ID: {def.Id}");
                    }
                    _definitions[def.Id] = def;
                }
                _logger.LogInformation("Loaded {Count} modifiers from {File}", list.Count, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to load modifiers from {File}", Path.GetFileName(filePath));
                throw;
            }
        }
    }
}