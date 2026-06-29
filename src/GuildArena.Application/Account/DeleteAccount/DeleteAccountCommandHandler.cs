using GuildArena.Application.Abstractions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Account.DeleteAccount;

/// <summary>
/// Handles the account deletion process, ensuring security validation,
/// Redis cleanup, and triggering EF Core cascade deletes.
/// </summary>
public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICombatStateRepository _combatStateRepo;
    private readonly ILogger<DeleteAccountCommandHandler> _logger;

    public DeleteAccountCommandHandler(
        ICurrentUserService currentUserService,
        UserManager<ApplicationUser> userManager,
        ICombatStateRepository combatStateRepo,
        ILogger<DeleteAccountCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userManager = userManager;
        _combatStateRepo = combatStateRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated.", ErrorType.Unauthorized));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure(new Error("Account.NotFound", "User account could not be found.", ErrorType.NotFound));
        }

        // 1. Security Check: Validate Password
        var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordCorrect)
        {
            _logger.LogWarning("Account deletion failed for user {UserId}: Invalid password provided.", userId);
            return Result.Failure(new Error("Account.InvalidPassword", "The provided password is incorrect.", ErrorType.Validation));
        }

        // 2. Redis Cleanup: Prevent zombie combat sessions
        await _combatStateRepo.ClearPlayerActiveCombatAsync(userId);

        // 3. Database Deletion: EF Core will handle the Cascade Deletes (Guild, Heroes, Quests, etc.)
        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to delete user {UserId}. Identity Errors: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));

            return Result.Failure(new Error("Account.DeletionFailed", "An error occurred while deleting the account from the database.", ErrorType.Failure));
        }

        _logger.LogInformation("User {UserId} successfully deleted their account.", userId);
        return Result.Success();
    }
}