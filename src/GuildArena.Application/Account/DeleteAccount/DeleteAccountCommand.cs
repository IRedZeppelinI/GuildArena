using GuildArena.Domain.Results;
using MediatR;

namespace GuildArena.Application.Account.DeleteAccount;

/// <summary>
/// Command to permanently delete the authenticated user's account and all associated data.
/// </summary>
public class DeleteAccountCommand : IRequest<Result>
{
    /// <summary>
    /// The user's current password for security verification.
    /// </summary>
    public required string Password { get; set; }
}