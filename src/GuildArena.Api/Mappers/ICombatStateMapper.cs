using GuildArena.Domain.Gameplay;
using GuildArena.Shared.DTOs.Combat;

namespace GuildArena.Api.Mappers;

/// <summary>
/// Defines the contract for mapping domain game states into secure DTOs for the client.
/// </summary>
public interface ICombatStateMapper
{
    /// <summary>
    /// Maps the root GameState to a GameStateDto, pre-calculating targeting rules and affordability.
    /// </summary>
    GameStateDto MapToDto(GameState state);
}