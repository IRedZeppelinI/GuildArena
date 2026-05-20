using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.GuildAndHeroes;

namespace GuildArena.Application.Abstractions;

/// <summary>
/// Orchestrates business logic related to Guilds, bridging database entities 
/// and static JSON definitions to generate comprehensive DTOs.
/// </summary>
public interface IGuildService
{
    /// <summary>
    /// Retrieves a player's roster of heroes, merging database state with static JSON names.
    /// </summary>
    Task<Result<List<HeroDto>>> GetGuildRosterAsync(int? guildId);

    /// <summary>
    /// Orchestrates the creation of a new guild and its starter pack.
    /// </summary>
    Task<Result> CreateGuildAsync(string applicationUserId, int? existingGuildId, string guildName);

    /// <summary>
    /// Retrieves the profile information for a user's guild.
    /// </summary>
    Task<Result<GuildProfileDto>> GetGuildProfileAsync(string applicationUserId);

}