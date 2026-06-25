using GuildArena.Application.Abstractions;
using GuildArena.Domain.Gameplay;

namespace GuildArena.IntegrationTests.TestInfrastructure;

public class InMemoryCombatStateRepository : ICombatStateRepository
{
    private readonly Dictionary<string, GameState> _store = new();

    // NOVO: Dicionário para simular os ponteiros do Redis (UserId -> CombatId)
    private readonly Dictionary<string, string> _playerPointers = new();

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

    public Task SetPlayerActiveCombatAsync(string userId, string combatId)
    {
        _playerPointers[userId] = combatId;
        return Task.CompletedTask;
    }

    public Task<string?> GetPlayerActiveCombatAsync(string userId)
    {
        return Task.FromResult(_playerPointers.TryGetValue(userId, out var combatId) ? combatId : null);
    }

    public Task ClearPlayerActiveCombatAsync(string userId)
    {
        _playerPointers.Remove(userId);
        return Task.CompletedTask;
    }
}