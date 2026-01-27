using GuildArena.Application.Abstractions;
using GuildArena.Domain.Gameplay;

namespace GuildArena.IntegrationTests.TestInfrastructure;

public class InMemoryCombatStateRepository : ICombatStateRepository
{
    private readonly Dictionary<string, GameState> _store = new();

    public Task<GameState?> GetAsync(string combatId)
        => Task.FromResult(_store.TryGetValue(combatId, out var s) ? s : null);

    public Task SaveAsync(string combatId, GameState state)
    {
        _store[combatId] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string combatId)
    {
        _store.Remove(combatId);
        return Task.CompletedTask;
    }
}
