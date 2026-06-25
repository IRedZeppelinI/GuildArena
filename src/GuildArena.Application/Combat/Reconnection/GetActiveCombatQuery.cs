using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Combat;
using MediatR;

namespace GuildArena.Application.Combat.Reconnection;

/// <summary>
/// Query to check if the currently authenticated user has an ongoing combat session stored in Redis.
/// </summary>
public class GetActiveCombatQuery : IRequest<Result<ActiveCombatDto>>
{
    // No properties needed. The user identity is extracted securely via ICurrentUserService in the handler.
}