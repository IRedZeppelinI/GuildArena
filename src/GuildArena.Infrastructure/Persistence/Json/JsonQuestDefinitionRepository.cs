using System.Collections.Frozen;
using System.Text.Json;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GuildArena.Infrastructure.Persistence.Json;

/// <summary>
/// Loads and indexes quest definitions from a JSON file at application startup.
/// Implements Fail Fast for duplicate definition IDs.
/// </summary>
public class JsonQuestDefinitionRepository : IQuestDefinitionRepository
{
    private readonly IReadOnlyDictionary<string, QuestDefinition> _definitions;
    private readonly ILogger<JsonQuestDefinitionRepository> _logger;

    public JsonQuestDefinitionRepository(
        IOptions<GameDataOptions> options,
        ILogger<JsonQuestDefinitionRepository> logger)
    {
        _logger = logger;
        var opts = options.Value;

        string filePath = Path.Combine(opts.AbsoluteRootPath, opts.QuestsFile);
        _logger.LogInformation("Loading quest definitions from {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Quest definitions file not found: {filePath}");
        }

        string json = File.ReadAllText(filePath);
        var list = JsonSerializer.Deserialize<List<QuestDefinition>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<QuestDefinition>();

        // Fail Fast on duplicate IDs
        var duplicates = list
            .GroupBy(q => q.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            throw new InvalidOperationException(
                $"Duplicate quest definition IDs found in {filePath}: {string.Join(", ", duplicates)}");
        }

        _definitions = list.ToFrozenDictionary(q => q.Id);
        _logger.LogInformation("Loaded {Count} quest definitions.", _definitions.Count);
    }

    /// <inheritdoc />
    public bool TryGetDefinition(string id, out QuestDefinition definition) =>
        _definitions.TryGetValue(id, out definition!);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, QuestDefinition> GetAllDefinitions() => _definitions;
}