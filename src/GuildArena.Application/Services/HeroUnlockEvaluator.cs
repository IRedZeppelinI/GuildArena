using GuildArena.Application.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.ValueObjects.UnlockHero;
using GuildArena.Shared.DTOs.Shop;

namespace GuildArena.Application.Services;

public class HeroUnlockEvaluator : IHeroUnlockEvaluator
{
    private readonly ICharacterDefinitionRepository _characterRepo;

    public HeroUnlockEvaluator(ICharacterDefinitionRepository characterRepo)
    {
        _characterRepo = characterRepo;
    }

    public bool AreConditionsMet(Guild guild, HeroUnlockRequirements requirements)
    {
        foreach (var condition in requirements.Conditions)
        {
            if (!IsConditionMet(guild, condition))
                return false;
        }
        return true;
    }

    public List<UnlockConditionDto> GetProgress(Guild guild, List<UnlockHeroCondition> conditions)
    {
        return conditions.Select(cond => new UnlockConditionDto
        {
            Type = cond.Type,
            RequiredValue = GetRequiredValue(cond),
            CurrentValue = GetCurrentValue(guild, cond),
            Description = cond.Description
        }).ToList();
    }

    private bool IsConditionMet(Guild guild, UnlockHeroCondition condition)
    {
        return condition.Type switch
        {
            UnlockConditionType.GuildLevel =>
                guild.Level >= (condition.MinLevel ?? 0),

            UnlockConditionType.MatchesPlayedWithRace =>
                GetMatchesPlayedWithRace(guild, condition.RaceId!) >= (condition.MinCount ?? 0),

            UnlockConditionType.MatchesWonWithRace =>
                GetMatchesWonWithRace(guild, condition.RaceId!) >= (condition.MinCount ?? 0),

            _ => false
        };
    }

    private int GetRequiredValue(UnlockHeroCondition condition)
    {
        return condition.Type switch
        {
            UnlockConditionType.GuildLevel => condition.MinLevel ?? 0,
            UnlockConditionType.MatchesPlayedWithRace => condition.MinCount ?? 0,
            UnlockConditionType.MatchesWonWithRace => condition.MinCount ?? 0,
            _ => 0
        };
    }

    private int GetCurrentValue(Guild guild, UnlockHeroCondition condition)
    {
        return condition.Type switch
        {
            UnlockConditionType.GuildLevel => guild.Level,
            UnlockConditionType.MatchesPlayedWithRace =>
                GetMatchesPlayedWithRace(guild, condition.RaceId!),
            UnlockConditionType.MatchesWonWithRace =>
                GetMatchesWonWithRace(guild, condition.RaceId!),
            _ => 0
        };
    }

    private int GetMatchesPlayedWithRace(Guild guild, string raceId)
    {
        if (guild.MatchHistory == null) return 0;

        return guild.MatchHistory
            .Where(mp => mp.HeroesUsed != null && mp.HeroesUsed.Any(hero =>
                IsRace(hero.HeroDefinitionId, raceId)))
            .Count();
    }

    private int GetMatchesWonWithRace(Guild guild, string raceId)
    {
        if (guild.MatchHistory == null) return 0;

        return guild.MatchHistory
            .Where(mp => mp.IsWinner &&
                         mp.HeroesUsed != null &&
                         mp.HeroesUsed.Any(hero => IsRace(hero.HeroDefinitionId, raceId)))
            .Count();
    }

    private bool IsRace(string heroDefinitionId, string raceId)
    {
        _characterRepo.TryGetDefinition(heroDefinitionId, out var def);
        return def?.RaceId == raceId;
    }
}