// FILE: \src\GuildArena.Application\Dungeons\GetActiveCampState\GetActiveCampStateQueryHandler.cs
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Results;
using GuildArena.Shared.DTOs.Dungeons;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Dungeons.GetActiveCampState;

/// <summary>
/// Returns the camp screen data if an active dungeon run exists.
/// </summary>
public class GetActiveCampStateQueryHandler : IRequestHandler<GetActiveCampStateQuery, Result<ActiveDungeonCampDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IDungeonRunRepository _dungeonRunRepo;
    private readonly IDungeonDefinitionRepository _dungeonDefRepo;
    private readonly ICharacterDefinitionRepository _charDefRepo;
    private readonly IRaceDefinitionRepository _raceDefRepo;
    private readonly ILogger<GetActiveCampStateQueryHandler> _logger;

    // The GuildRepository already can return heroes. We'll use IGuildRepository.GetAllHeroesAsync.
    public GetActiveCampStateQueryHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IDungeonRunRepository dungeonRunRepo,
        IDungeonDefinitionRepository dungeonDefRepo,
        ICharacterDefinitionRepository charDefRepo,
        IRaceDefinitionRepository raceDefRepo,
        ILogger<GetActiveCampStateQueryHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _dungeonRunRepo = dungeonRunRepo;
        _dungeonDefRepo = dungeonDefRepo;
        _charDefRepo = charDefRepo;
        _raceDefRepo = raceDefRepo;
        _logger = logger;
    }

    public async Task<Result<ActiveDungeonCampDto>> Handle(GetActiveCampStateQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<ActiveDungeonCampDto>(new Error("Auth.Unauthorized", "User not authenticated.", ErrorType.Unauthorized));

        var guild = await _guildRepo.GetGuildByUserIdAsync(userId);
        if (guild == null)
            return Result.Failure<ActiveDungeonCampDto>(new Error("Dungeon.NoGuild", "No guild found.", ErrorType.NotFound));

        var activeRun = await _dungeonRunRepo.GetActiveRunAsync(guild.Id, cancellationToken);
        if (activeRun == null)
            return Result.Failure<ActiveDungeonCampDto>(new Error("Dungeon.NoActiveRun", "No active dungeon run.", ErrorType.NotFound));

        if (!_dungeonDefRepo.TryGetDefinition(activeRun.DungeonDefinitionId, out var dungeonDef))
            return Result.Failure<ActiveDungeonCampDto>(new Error("Dungeon.DefinitionNotFound", "Dungeon definition missing.", ErrorType.NotFound));

        var stage = dungeonDef.Stages.SingleOrDefault(s => s.StageIndex == activeRun.CurrentStageIndex);
        if (stage == null)
            return Result.Failure<ActiveDungeonCampDto>(new Error("Dungeon.InvalidState", "Current stage index invalid.", ErrorType.NotFound));

        // Load the heroes of the guild
        var heroes = await _guildRepo.GetAllHeroesAsync(guild.Id);
        var campHeroes = new List<CampHeroDto>();

        foreach (var heroState in activeRun.HeroesState)
        {
            var hero = heroes.FirstOrDefault(h => h.Id == heroState.HeroId);
            if (hero == null) continue; // should not happen

            string heroName = _charDefRepo.TryGetDefinition(hero.CharacterDefinitionId, out var charDef)
                ? charDef.Name
                : "Unknown";

            int maxHp = ComputeMaxHp(hero);
            campHeroes.Add(new CampHeroDto
            {
                HeroId = hero.Id,
                Name = heroName,
                CurrentHP = heroState.CurrentHP,
                MaxHP = maxHp
            });
        }

        return new ActiveDungeonCampDto
        {
            DungeonId = activeRun.DungeonDefinitionId,
            DungeonName = dungeonDef.Name,
            CurrentStageIndex = activeRun.CurrentStageIndex,
            IsBossNode = stage.IsBossNode,
            Heroes = campHeroes
        };
    }

    private int ComputeMaxHp(GuildArena.Domain.Entities.Hero hero)
    {
        if (!_charDefRepo.TryGetDefinition(hero.CharacterDefinitionId, out var charDef))
            return 1;
        if (!_raceDefRepo.TryGetDefinition(charDef.RaceId, out var raceDef))
            return 1;

        int level = hero.CurrentLevel;
        int levelsToScale = Math.Max(0, level - 1);
        float baseMaxHp = charDef.BaseStats.MaxHP + raceDef.BonusStats.MaxHP
                          + (charDef.StatsGrowthPerLevel.MaxHP * levelsToScale);
        return Math.Max(1, (int)baseMaxHp);
    }
}