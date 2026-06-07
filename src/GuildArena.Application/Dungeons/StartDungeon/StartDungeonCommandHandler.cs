// FILE: \src\GuildArena.Application\Dungeons\StartDungeon\StartDungeonCommandHandler.cs
using GuildArena.Application.Abstractions;
using GuildArena.Application.Abstractions.Repositories;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.UnlockHero;
using GuildArena.Domain.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Dungeons.StartDungeon;

/// <summary>
/// Handles the creation of a new dungeon run: validates eligibility,
/// checks for an existing active run, creates the run entity with initial hero HP states.
/// </summary>
public class StartDungeonCommandHandler : IRequestHandler<StartDungeonCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGuildRepository _guildRepo;
    private readonly IDungeonRunRepository _dungeonRunRepo;
    private readonly IDungeonDefinitionRepository _dungeonDefRepo;
    private readonly ICharacterDefinitionRepository _characterDefRepo;
    private readonly IRaceDefinitionRepository _raceDefRepo;
    private readonly ILogger<StartDungeonCommandHandler> _logger;

    public StartDungeonCommandHandler(
        ICurrentUserService currentUser,
        IGuildRepository guildRepo,
        IDungeonRunRepository dungeonRunRepo,
        IDungeonDefinitionRepository dungeonDefRepo,
        ICharacterDefinitionRepository characterDefRepo,
        IRaceDefinitionRepository raceDefRepo,
        ILogger<StartDungeonCommandHandler> logger)
    {
        _currentUser = currentUser;
        _guildRepo = guildRepo;
        _dungeonRunRepo = dungeonRunRepo;
        _dungeonDefRepo = dungeonDefRepo;
        _characterDefRepo = characterDefRepo;
        _raceDefRepo = raceDefRepo;
        _logger = logger;
    }

    public async Task<Result> Handle(StartDungeonCommand request, CancellationToken cancellationToken)
    {
        // 1. Authentication
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(new Error("Auth.Unauthorized", "User is not authenticated.", ErrorType.Unauthorized));

        // 2. Guild must exist
        var guild = await _guildRepo.GetGuildByUserIdAsync(userId);
        if (guild == null)
            return Result.Failure(new Error("Dungeon.NoGuild", "You must create a guild first.", ErrorType.NotFound));

        // 3. Hero validation (must own exactly 3)
        var heroes = await _guildRepo.GetHeroesAsync(guild.Id, request.HeroInstanceIds);
        if (heroes.Count != 3)
            return Result.Failure(new Error(
                "Dungeon.InvalidHeroCount",
                $"Dungeon runs require exactly 3 heroes. You provided {heroes.Count}.",
                ErrorType.Validation));

        // 4. No active run may exist
        var existingRun = await _dungeonRunRepo.GetActiveRunAsync(guild.Id, cancellationToken);
        if (existingRun != null)
            return Result.Failure(new Error(
                "Dungeon.ActiveRunExists",
                "You already have an active dungeon run. Complete or abandon it before starting a new one.",
                ErrorType.Conflict));

        // 5. Dungeon definition must be valid
        if (!_dungeonDefRepo.TryGetDefinition(request.DungeonId, out var dungeonDef))
            return Result.Failure(new Error(
                "Dungeon.NotFound",
                $"Dungeon '{request.DungeonId}' does not exist.",
                ErrorType.NotFound));

        // 6. Guild level requirement
        if (guild.Level < dungeonDef.RequiredGuildLevel)
            return Result.Failure(new Error(
                "Dungeon.LevelTooLow",
                $"Your guild must be at least level {dungeonDef.RequiredGuildLevel} to enter this dungeon.",
                ErrorType.Forbidden));

        // 7. Create the ActiveDungeonRun entity
        var run = new ActiveDungeonRun
        {
            GuildId = guild.Id,
            DungeonDefinitionId = request.DungeonId,
            CurrentStageIndex = 0,
            HeroesState = new List<DungeonHeroState>()
        };

        // 8. Compute initial HP for each hero (full HP based on current level)
        foreach (var hero in heroes)
        {
            int maxHp = ComputeMaxHp(hero);
            run.HeroesState.Add(new DungeonHeroState
            {
                HeroId = hero.Id,
                CurrentHP = maxHp
            });
        }

        // 9. Persist
        await _dungeonRunRepo.CreateRunAsync(run, cancellationToken);

        _logger.LogInformation("Dungeon run started for guild {GuildId} in dungeon {DungeonId}.",
            guild.Id, request.DungeonId);

        return Result.Success();
    }

    /// <summary>
    /// Computes the hero's maximum HP at their current level using the same scaling formulas as <see cref="Core.Combat.Factories.CombatantFactory"/>.
    /// </summary>
    private int ComputeMaxHp(Hero hero)
    {
        if (!_characterDefRepo.TryGetDefinition(hero.CharacterDefinitionId, out var charDef))
            throw new InvalidOperationException($"Character definition '{hero.CharacterDefinitionId}' not found.");

        if (!_raceDefRepo.TryGetDefinition(charDef.RaceId, out var raceDef))
            throw new InvalidOperationException($"Race '{charDef.RaceId}' not found.");

        int level = hero.CurrentLevel;
        int levelsToScale = Math.Max(0, level - 1);

        float baseMaxHp = charDef.BaseStats.MaxHP + raceDef.BonusStats.MaxHP
                          + (charDef.StatsGrowthPerLevel.MaxHP * levelsToScale);

        return Math.Max(1, (int)baseMaxHp);
    }
}