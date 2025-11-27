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
        var filePath = Path.Combine(_options.AbsoluteRootPath, _options.ModifiersFile);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Modifiers file not found at: {filePath}");
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var serializerOptions = JsonOptionsFactory.Create(); // us ao helper
            var list = JsonSerializer.Deserialize<List<ModifierDefinition>>(jsonContent, serializerOptions);

            if (list == null) throw new Exception("JSON deserialization returned null.");

            foreach (var def in list)
            {
                if (_definitions.ContainsKey(def.Id))
                {
                    _logger.LogError("Duplicate Modifier ID: {Id}", def.Id);
                    continue;
                }
                _definitions[def.Id] = def;
            }

            _logger.LogInformation("Loaded {Count} modifiers from {Path}", _definitions.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load modifiers.");
            throw; // Propaga o erro para o Warmup no Program.cs
        }
    }
}