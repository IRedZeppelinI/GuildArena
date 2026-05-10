using GuildArena.Application.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Domain.ValueObjects.Stats;
using GuildArena.Shared.DTOs.Combat;
using GuildArena.Shared.DTOs.GuildAndHeroes;

namespace GuildArena.Application.Services;

public class CharacterService : ICharacterService
{
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IRaceDefinitionRepository _raceRepo;
    private readonly IAbilityDefinitionRepository _abilityRepo;

    public CharacterService(
        ICharacterDefinitionRepository characterRepo,
        IRaceDefinitionRepository raceRepo,
        IAbilityDefinitionRepository abilityRepo)
    {
        _characterRepo = characterRepo;
        _raceRepo = raceRepo;
        _abilityRepo = abilityRepo;
    }

    public Result<HeroDetailsDto> GetCharacterDetails(string definitionId)
    {
        if (!_characterRepo.TryGetDefinition(definitionId, out var charDef))
        {
            return Result.Failure<HeroDetailsDto>(new Error("Character.NotFound", "Character definition missing.", ErrorType.NotFound));
        }

        _raceRepo.TryGetDefinition(charDef.RaceId, out var raceDef);

        
        var abilities = new List<AbilitySummaryDto>();

        void AddAbility(string? abId)
        {
            if (!string.IsNullOrEmpty(abId) && _abilityRepo.TryGetDefinition(abId, out var abDef))
            {
                abilities.Add(new AbilitySummaryDto
                {
                    Id = abDef.Id,
                    Name = abDef.Name,
                    Description = abDef.Description ?? string.Empty,
                    ActionPointCost = abDef.ActionPointCost,
                    BaseCooldown = abDef.BaseCooldown,
                    HPCost = abDef.HPCost,
                    Costs = abDef.Costs.ToDictionary(c => c.Type, c => c.Amount),
                    Tags = abDef.Tags.ToList()
                });
            }
        }

        AddAbility(charDef.GuardAbilityId ?? charDef.FocusAbilityId);
        foreach (var abId in charDef.AbilityIds) AddAbility(abId);

        float GetStat(Func<BaseStats, float> statSelector)
        {
            return statSelector(charDef.BaseStats) + (raceDef != null ? statSelector(raceDef.BonusStats) : 0);
        }

        return new HeroDetailsDto
        {
            Id = 0,
            DefinitionId = charDef.Id,
            Name = charDef.Name,
            RaceName = raceDef?.Name ?? "Unknown Race",
            CurrentLevel = 1,
            MaxHP = (int)GetStat(s => s.MaxHP),
            Attack = (int)GetStat(s => s.Attack),
            Defense = (int)GetStat(s => s.Defense),
            Agility = (int)GetStat(s => s.Agility),
            Magic = (int)GetStat(s => s.Magic),
            MagicDefense = (int)GetStat(s => s.MagicDefense),
            Abilities = abilities
        };
    }
}