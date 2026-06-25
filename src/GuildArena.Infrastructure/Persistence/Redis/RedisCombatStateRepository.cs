using GuildArena.Application.Abstractions;
using GuildArena.Domain.Gameplay;
using StackExchange.Redis;
using System.Text.Json;

namespace GuildArena.Infrastructure.Persistence.Redis;

public class RedisCombatStateRepository : ICombatStateRepository
{
    private readonly IDatabase _database;
    // Tempo de vida para os combates "mortos"
    private readonly TimeSpan _timeToLive = TimeSpan.FromHours(1);

    public RedisCombatStateRepository(IConnectionMultiplexer redisConnection)
    {
        // Obtém a ligação à base de dados do Redis
        _database = redisConnection.GetDatabase();
    }

    /// <summary>
    /// Gets the full combat state from persistence.
    /// </summary>
    public async Task<GameState?> GetAsync(string combatId)
    {
        var key = GetKey(combatId);

        // Vai ao Redis e pede o valor da string
        RedisValue jsonState = await _database.StringGetAsync(key);

        if (jsonState.IsNullOrEmpty)
        {
            return null;
        }

        // Desserializa o JSON de volta para o objeto GameState
        return JsonSerializer.Deserialize<GameState>(jsonState!);
    }

    /// <summary>
    /// Saves (or overwrites) the full combat state.
    /// </summary>
    public async Task SaveAsync(string combatId, GameState state)
    {
        var key = GetKey(combatId);

        // Serializa o objeto GameState para uma string JSON
        var jsonState = JsonSerializer.Serialize(state);

        // Guarda no Redis com o tempo de vida (TTL)
        await _database.StringSetAsync(key, jsonState, _timeToLive);
    }

    /// <summary>
    /// Removes a combat state from persistence (e.g., at the end of combat).
    /// </summary>
    public async Task DeleteAsync(string combatId)
    {
        var key = GetKey(combatId);
        await _database.KeyDeleteAsync(key);
    }
        

    public async Task SetPlayerActiveCombatAsync(string userId, string combatId)
    {
        var key = $"player_combat:{userId}";
        // Guarda o ponteiro com o mesmo tempo de vida do combate
        await _database.StringSetAsync(key, combatId, _timeToLive);
    }

    public async Task<string?> GetPlayerActiveCombatAsync(string userId)
    {
        var key = $"player_combat:{userId}";
        RedisValue combatId = await _database.StringGetAsync(key);

        return combatId.HasValue ? combatId.ToString() : null;
    }

    public async Task ClearPlayerActiveCombatAsync(string userId)
    {
        var key = $"player_combat:{userId}";
        await _database.KeyDeleteAsync(key);
    }


    // Método helper privado para garantir que as chaves têm um padrão
    private string GetKey(string combatId) => $"combat:{combatId}";
}
