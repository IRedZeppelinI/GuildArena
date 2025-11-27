using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuildArena.Infrastructure.Persistence.Json;

/// <summary>
/// Centralizes the JSON serialization configuration to ensure consistency
/// across all data repositories (Abilities, Modifiers, etc.).
/// </summary>
public static class JsonOptionsFactory
{
    /// <summary>
    /// Creates the standard options used to deserialize game data.
    /// Handles Enum-to-String conversion and case insensitivity.
    /// </summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            // lidar com casing
            PropertyNameCaseInsensitive = true,

            // Permite vírgulas no final de listas
            AllowTrailingCommas = true,

            // Permite comentários no JSON (// ou /* */) 
            ReadCommentHandling = JsonCommentHandling.Skip,

            // Formatação com identação para facilitar leitura se fizer write 
            WriteIndented = true
        };

        // Conversor enum - string
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}