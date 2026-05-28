using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Shop;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Tavern.GetTavernHero;

public class GetTavernHeroQueryHandler : IRequestHandler<GetTavernHeroQuery, Result<TavernHeroDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IHeroUnlockEvaluator _unlockEvaluator;
    private readonly ILogger<GetTavernHeroQueryHandler> _logger;

    public GetTavernHeroQueryHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        ICharacterDefinitionRepository characterRepo,
        IRaceDefinitionRepository raceRepo,
        IHeroUnlockEvaluator unlockEvaluator,
        ILogger<GetTavernHeroQueryHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _characterRepo = characterRepo;
        _raceRepo = raceRepo;
        _unlockEvaluator = unlockEvaluator;
        _logger = logger;
    }

    public async Task<Result<TavernHeroDto>> Handle(GetTavernHeroQuery request, CancellationToken cancellationToken)
    {
        // 1. User must have a guild
        if (!_currentUser.GuildId.HasValue)
        {
            return Result.Failure<TavernHeroDto>(new Error(
                "Tavern.NoGuild",
                "User is not associated with a guild.",
                ErrorType.NotFound));
        }

        var guild = await _guildRepo.GetGuildWithHistoryAsync(_currentUser.UserId!);
        if (guild == null)
        {
            return Result.Failure<TavernHeroDto>(new Error(
                "Tavern.GuildNotFound",
                "Guild not found for the current user.",
                ErrorType.NotFound));
        }

        // 2. Hero definition must exist and be a hero
        if (!_characterRepo.TryGetDefinition(request.DefinitionId, out var heroDef)
            || !request.DefinitionId.StartsWith("HERO_", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<TavernHeroDto>(new Error(
                "Tavern.HeroNotFound",
                $"Hero definition '{request.DefinitionId}' not found.",
                ErrorType.NotFound));
        }

        string raceName = _raceRepo.TryGetDefinition(heroDef.RaceId, out var raceDef)
            ? raceDef.Name
            : "Unknown";

        // 3. Check if already owned
        var ownedHeroes = await _guildRepo.GetAllHeroesAsync(guild.Id); 
        var owned = guild.Heroes.FirstOrDefault(h => h.CharacterDefinitionId == request.DefinitionId);
        if (owned != null)
        {
            // Already owned – return with Status=Owned, and the actual hero Id
            return new TavernHeroDto
            {
                Id = owned.Id,
                DefinitionId = owned.CharacterDefinitionId,
                Name = heroDef.Name,
                RaceName = raceName,
                GoldCost = heroDef.UnlockRequirements?.GoldCost ?? 0,   // Still show cost for info
                Status = HeroStatus.Owned
            };
        }

        // 4. Not owned – evaluate unlock
        var dto = new TavernHeroDto
        {
            DefinitionId = request.DefinitionId,
            Name = heroDef.Name,
            RaceName = raceName,
            GoldCost = heroDef.UnlockRequirements?.GoldCost ?? 0
        };

        // Código só chega aqui para heróis que não pertençam já à guild, mas numa
        //óptica de programação defensiva substitui Available apra entregar erro caso
        //exista herói sem UnlockRequirements que não sejam owned (só os do starter pack
        // não têm UnlockRequirements)
        //if (heroDef.UnlockRequirements == null)
        //{
        //    // Starter hero without requirements (should not happen, but treat as Available)
        //    dto.Status = HeroStatus.Available;
        //}
        if (heroDef.UnlockRequirements == null)
        {
            return Result.Failure<TavernHeroDto>(new Error(
                "Tavern.InvalidHero",
                "This hero cannot be purchased or is in an inconsistent state.",
                ErrorType.Validation));
        }
        else
        {
            bool conditionsMet = _unlockEvaluator.AreConditionsMet(guild, heroDef.UnlockRequirements);
            dto.Status = conditionsMet ? HeroStatus.Available : HeroStatus.Locked;
            dto.UnlockConditions = _unlockEvaluator.GetProgress(guild, heroDef.UnlockRequirements.Conditions);
        }

        return dto;
    }
}