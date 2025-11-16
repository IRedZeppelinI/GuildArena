using GuildArena.Domain.Definitions;

namespace GuildArena.Domain.Abstractions.Repositories;

/// <summary>
/// Defines the contract for retrieving static Modifier definitions (the "blueprints").
/// </summary>
public interface IModifierDefinitionRepository
{
    // Usamos um Dicionário para acesso O(1) ultra-rápido pelo ID.
    // A implementação disto (no futuro) vai ler o JSON e metê-lo na cache.
    IReadOnlyDictionary<string, ModifierDefinition> GetAllDefinitions();
}