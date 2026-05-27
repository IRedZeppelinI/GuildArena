using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Tavern.GetTavernShop;

public class GetTavernShopQueryHandler : IRequestHandler<GetTavernShopQuery, Result<TavernShopDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IHeroUnlockEvaluator _unlockEvaluator;
    private readonly ILogger<GetTavernShopQueryHandler> _logger;

    public GetTavernShopQueryHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        ICharacterDefinitionRepository characterRepo,
        IRaceDefinitionRepository raceRepo,
        IHeroUnlockEvaluator unlockEvaluator,
        ILogger<GetTavernShopQueryHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _characterRepo = characterRepo;
        _raceRepo = raceRepo;
        _unlockEvaluator = unlockEvaluator;
        _logger = logger;
    }

    public async Task<Result<TavernShopDto>> Handle(GetTavernShopQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.GuildId.HasValue)
        {
            return Result.Failure<TavernShopDto>(new Error(
                "Tavern.NoGuild",
                "User is not associated with a guild.",
                ErrorType.NotFound));
        }

        var guild = await _guildRepo.GetGuildWithHistoryAsync(_currentUser.UserId!);
        if (guild == null)
        {
            return Result.Failure<TavernShopDto>(new Error(
                "Tavern.GuildNotFound",
                "Guild not found for the current user.",
                ErrorType.NotFound));
        }

        var allDefinitions = _characterRepo.GetAllDefinitions();
        var ownedHeroes = await _guildRepo.GetAllHeroesAsync(guild.Id);
        var ownedDefinitionIds = ownedHeroes.Select(h => h.CharacterDefinitionId).ToHashSet();

        var heroDtos = new List<TavernHeroDto>();

        foreach (var def in allDefinitions.Values)
        {
            // Only hero definitions (not mobs) are displayed in the tavern
            if (!def.Id.StartsWith("HERO_", StringComparison.OrdinalIgnoreCase))
                continue;

            string raceName = _raceRepo.TryGetDefinition(def.RaceId, out var raceDef)
                ? raceDef.Name
                : "Unknown";

            var dto = new TavernHeroDto
            {
                DefinitionId = def.Id,
                Name = def.Name,
                RaceName = raceName,
                GoldCost = def.UnlockRequirements?.GoldCost ?? 0,
            };

            if (ownedDefinitionIds.Contains(def.Id))
            {
                var owned = ownedHeroes.First(h => h.CharacterDefinitionId == def.Id);
                dto.Id = owned.Id;
                dto.Status = HeroStatus.Owned;
            }
            else if (def.UnlockRequirements != null)
            {
                bool conditionsMet = _unlockEvaluator.AreConditionsMet(guild, def.UnlockRequirements);
                dto.Status = conditionsMet ? HeroStatus.Available : HeroStatus.Locked;
                dto.UnlockConditions = _unlockEvaluator.GetProgress(guild, def.UnlockRequirements.Conditions);
            }
            else
            {
                // Hero without unlock requirements that is not owned – should not happen
                // (starters are granted at guild creation), but handle gracefully.
                dto.Status = HeroStatus.Available;
            }

            heroDtos.Add(dto);
        }

        return new TavernShopDto
        {
            GuildGold = guild.Gold,
            Heroes = heroDtos
        };
    }
}