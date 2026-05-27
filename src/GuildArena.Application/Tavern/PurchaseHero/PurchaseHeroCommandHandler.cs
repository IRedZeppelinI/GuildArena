using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Tavern.PurchaseHero;

public class PurchaseHeroCommandHandler : IRequestHandler<PurchaseHeroCommand, Result<PurchaseHeroResponse>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IHeroUnlockEvaluator _unlockEvaluator;
    private readonly IHeroPurchaseRepository _purchaseRepo;
    private readonly ILogger<PurchaseHeroCommandHandler> _logger;

    public PurchaseHeroCommandHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        ICharacterDefinitionRepository characterRepo,
        IHeroUnlockEvaluator unlockEvaluator,
        IHeroPurchaseRepository purchaseRepo,
        ILogger<PurchaseHeroCommandHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _characterRepo = characterRepo;
        _unlockEvaluator = unlockEvaluator;
        _purchaseRepo = purchaseRepo;
        _logger = logger;
    }

    public async Task<Result<PurchaseHeroResponse>> Handle(PurchaseHeroCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticated user must have a guild
        if (!_currentUser.GuildId.HasValue)
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.NoGuild",
                "You must belong to a guild to purchase heroes.",
                ErrorType.Forbidden));
        }

        var guild = await _guildRepo.GetGuildWithHistoryAsync(_currentUser.UserId!);
        if (guild == null)
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.GuildNotFound",
                "Guild not found for the current user.",
                ErrorType.NotFound));
        }

        // 2. Hero definition must exist
        if (!_characterRepo.TryGetDefinition(request.HeroId, out var heroDef))
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.HeroNotFound",
                $"Hero definition '{request.HeroId}' does not exist.",
                ErrorType.NotFound));
        }

        // 3. Hero must have unlock requirements (cannot buy starters)
        if (heroDef.UnlockRequirements == null)
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.StarterHero",
                "This hero is a starter and cannot be purchased.",
                ErrorType.Validation));
        }

        // 4. Guild must not already own the hero
        var ownedHeroes = await _guildRepo.GetAllHeroesAsync(guild.Id);
        if (ownedHeroes.Any(h => h.CharacterDefinitionId == request.HeroId))
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.AlreadyOwned",
                "Your guild already owns this hero.",
                ErrorType.Conflict));
        }

        // 5. Unlock conditions must be met
        if (!_unlockEvaluator.AreConditionsMet(guild, heroDef.UnlockRequirements))
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.ConditionsNotMet",
                "You do not meet the requirements to unlock this hero.",
                ErrorType.Validation));
        }

        // 6. Enough gold
        int cost = heroDef.UnlockRequirements.GoldCost;
        if (guild.Gold < cost)
        {
            return Result.Failure<PurchaseHeroResponse>(new Error(
                "Purchase.InsufficientGold",
                $"Purchasing this hero costs {cost} gold, but your guild only has {guild.Gold}.",
                ErrorType.Validation));
        }

        // --- Execute purchase ---

        // Deduct gold and update guild
        guild.Gold -= cost;

        // Add hero to guild
        var newHero = new Hero
        {
            GuildId = guild.Id,
            CharacterDefinitionId = request.HeroId,
            CurrentLevel = 1,
            CurrentXP = 0
        };
        guild.Heroes.Add(newHero);

        // Record transaction
        var purchaseRecord = new HeroPurchase
        {
            GuildId = guild.Id,
            CharacterDefinitionId = request.HeroId,
            GoldPaid = cost,
            PurchasedAt = DateTime.UtcNow
        };
        await _purchaseRepo.AddAsync(purchaseRecord, cancellationToken);


        // Persist guild changes (gold deduction + new hero)
        await _guildRepo.UpdateGuildAsync(guild);

        _logger.LogInformation("Guild {GuildId} purchased hero {HeroId} for {Cost} gold.",
            guild.Id, request.HeroId, cost);



        return new PurchaseHeroResponse
        {
            Success = true,
            UpdatedGold = guild.Gold
        };
    }
}