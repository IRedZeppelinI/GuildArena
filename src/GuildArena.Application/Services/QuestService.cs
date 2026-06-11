// FILE: src/GuildArena.Application/Services/QuestService.cs

using GuildArena.Application.Abstractions;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Quests;
using GuildArena.Domain.Results;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Services;

/// <inheritdoc />
public class QuestService : IQuestService
{
    private readonly IQuestDefinitionRepository _questRepo;
    private readonly ICharacterDefinitionRepository _characterRepo;
    private readonly IGuildProgressionService _progressionService;
    private readonly IRandomProvider _random;
    private readonly ILogger<QuestService> _logger;

    private const int MaxActiveQuests = 3;

    /// <summary>
    /// Initializes a new instance of <see cref="QuestService"/>.
    /// </summary>
    public QuestService(
        IQuestDefinitionRepository questRepo,
        ICharacterDefinitionRepository characterRepo,
        IGuildProgressionService progressionService,
        IRandomProvider random,
        ILogger<QuestService> logger)
    {
        _questRepo = questRepo;
        _characterRepo = characterRepo;
        _progressionService = progressionService;
        _random = random;
        _logger = logger;
    }

    // ─── public members ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task GrantDailyQuestsIfNeededAsync(Guild guild)
    {
        var today = DateTime.UtcNow.Date;

        // Já processou as quests hoje?
        if (guild.LastDailyQuestGrantedAt.HasValue &&
            guild.LastDailyQuestGrantedAt.Value.Date >= today)
        {
            return;
        }

        // Quantos slots livres temos?
        int activeCount = guild.ActiveQuests.Count(q => !q.IsCompleted);
        int slotsAvailable = MaxActiveQuests - activeCount;

        if (slotsAvailable <= 0)
        {
            // O Log está cheio, mas atualizamos a data para não voltar a verificar hoje
            guild.LastDailyQuestGrantedAt = DateTime.UtcNow;
            return;
        }

        //  1 Quest por Dia (Acumula se não fizer login) 
        int questsToGrant = 1;

        if (guild.LastDailyQuestGrantedAt.HasValue)
        {
            // Se ele não entra no jogo há 2 dias, ganha 2 quests (até ao limite de slots)
            int missedDays = (int)(today - guild.LastDailyQuestGrantedAt.Value.Date).TotalDays;
            questsToGrant = missedDays;
        }

        // Nunca podemos dar mais quests do que os slots disponíveis
        questsToGrant = Math.Min(questsToGrant, slotsAvailable);

        if (questsToGrant <= 0)
        {
            guild.LastDailyQuestGrantedAt = DateTime.UtcNow;
            return;
        }
        // ------------------------------------------------------------------

        _logger.LogInformation("Granting {Count} daily quests to Guild {GuildId}. Slots available: {Slots}",
            questsToGrant, guild.Id, slotsAvailable);

        var candidatePool = GetFilteredQuestDefinitions(guild);

        int granted = 0;
        // O ciclo agora obedece à variável questsToGrant em vez do slotsAvailable
        while (granted < questsToGrant && candidatePool.Count > 0)
        {
            int index = _random.Next(candidatePool.Count);
            var pickedDef = candidatePool[index];

            var newQuest = new ActiveQuest
            {
                GuildId = guild.Id,
                QuestDefinitionId = pickedDef.Id,
                CurrentProgress = 0
            };

            guild.ActiveQuests.Add(newQuest);
            candidatePool.RemoveAt(index); // Evitar dar a mesma quest 2 vezes no mesmo dia
            granted++;
        }

        guild.LastDailyQuestGrantedAt = DateTime.UtcNow;
        _logger.LogInformation("Granted {Count} daily quests to Guild {GuildId}.", granted, guild.Id);
    }

    /// <inheritdoc />
    public async Task<Result> RerollQuestAsync(Guild guild, int activeQuestId)
    {
        var quest = guild.ActiveQuests.FirstOrDefault(q => q.Id == activeQuestId);
        if (quest == null)
        {
            return Result.Failure(new Error(
                "Quests.NotFound",
                "Quest not found.",
                ErrorType.NotFound));
        }

        if (quest.IsCompleted)
        {
            return Result.Failure(new Error(
                "Quests.AlreadyCompleted",
                "Cannot reroll a completed quest.",
                ErrorType.Validation));
        }

        var today = DateTime.UtcNow.Date;
        if (guild.LastQuestRerollAt.HasValue &&
            guild.LastQuestRerollAt.Value.Date >= today)
        {
            return Result.Failure(new Error(
                "Quests.RerollUsed",
                "You have already rerolled a quest today.",
                ErrorType.Conflict));
        }

        // Remove the old quest
        guild.ActiveQuests.Remove(quest);

        // Pick a new one (smart filter)
        var candidatePool = GetFilteredQuestDefinitions(guild);

        // Exclude current active definition IDs to avoid duplicates
        var activeDefIds = guild.ActiveQuests
            .Where(q => !q.IsCompleted)
            .Select(q => q.QuestDefinitionId)
            .ToHashSet();
        candidatePool = candidatePool
            .Where(d => !activeDefIds.Contains(d.Id))
            .ToList();

        if (candidatePool.Count == 0)
        {
            _logger.LogWarning("No valid quests to reroll into for Guild {GuildId}.", guild.Id);
            return Result.Failure(new Error(
                "Quests.NoValidPool",
                "No available quests match your guild's current state.",
                ErrorType.Validation));
        }

        int index = _random.Next(candidatePool.Count);
        var pickedDef = candidatePool[index];

        var newQuest = new ActiveQuest
        {
            GuildId = guild.Id,
            QuestDefinitionId = pickedDef.Id,
            CurrentProgress = 0
        };
        guild.ActiveQuests.Add(newQuest);

        guild.LastQuestRerollAt = DateTime.UtcNow;
        _logger.LogInformation("Guild {GuildId} rerolled quest {OldId}. New: {NewId}",
            guild.Id, quest.Id, newQuest.QuestDefinitionId);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task ProcessMatchEndAsync(Guild guild, Match match, bool isWinner)
    {
        var activeQuests = guild.ActiveQuests.Where(q => !q.IsCompleted).ToList();
        if (!activeQuests.Any()) return;

        var allDefs = _questRepo.GetAllDefinitions();

        // Identify the guild's participation in this match
        var guildParticipation = match.Participants
            .FirstOrDefault(p => p.GuildId == guild.Id);

        if (guildParticipation == null)
        {
            _logger.LogWarning(
                "Guild {GuildId} not found among match participants. Skipping quest progress.",
                guild.Id);
            return;
        }

        foreach (var quest in activeQuests)
        {
            if (!allDefs.TryGetValue(quest.QuestDefinitionId, out var def))
                continue;

            bool progressed = false;

            switch (def.RequirementType)
            {
                case QuestRequirementType.WinMatch:
                    if (isWinner) progressed = true;
                    break;

                case QuestRequirementType.PlayMatch:
                    progressed = true;
                    break;

                case QuestRequirementType.PlayWithRace:
                    if (!string.IsNullOrEmpty(def.RequiredRaceId))
                    {
                        bool raceUsed = guildParticipation.HeroesUsed
                            .Any(heroEntry => IsHeroOfRace(heroEntry.HeroDefinitionId, def.RequiredRaceId));
                        if (raceUsed) progressed = true;
                    }
                    break;

                case QuestRequirementType.PlayWithHero:
                    if (!string.IsNullOrEmpty(def.RequiredHeroDefinitionId))
                    {
                        bool heroUsed = guildParticipation.HeroesUsed
                            .Any(heroEntry =>
                                heroEntry.HeroDefinitionId == def.RequiredHeroDefinitionId);
                        if (heroUsed) progressed = true;
                    }
                    break;
            }

            if (progressed)
            {
                quest.CurrentProgress++;
                _logger.LogDebug(
                    "Quest {QuestId} progressed to {Progress}/{Target}.",
                    quest.Id, quest.CurrentProgress, def.TargetValue);

                if (quest.CurrentProgress >= def.TargetValue)
                {
                    CompleteQuest(quest, def, guild);
                }
            }
        }
    }

    // ─── private helpers ────────────────────────────────────────────

    /// <summary>
    /// Returns all quest definitions that are valid for the guild based on owned heroes/races.
    /// </summary>
    private List<QuestDefinition> GetFilteredQuestDefinitions(Guild guild)
    {
        var allDefs = _questRepo.GetAllDefinitions().Values.ToList();

        // Exclude quests already active (incomplete)
        var activeDefIds = guild.ActiveQuests
            .Where(q => !q.IsCompleted)
            .Select(q => q.QuestDefinitionId)
            .ToHashSet();

        // Pre-fetch hero character definitions to check races
        var heroDefinitions = guild.Heroes
            .Select(h =>
            {
                _characterRepo.TryGetDefinition(h.CharacterDefinitionId, out var def);
                return (HeroId: h.Id, Definition: def);
            })
            .ToList();

        return allDefs
            .Where(def =>
            {
                // Already active? skip
                if (activeDefIds.Contains(def.Id)) return false;

                // If no special requirement, always valid
                bool valid = true;

                if (!string.IsNullOrEmpty(def.RequiredRaceId))
                {
                    valid = heroDefinitions.Any(hd =>
                        hd.Definition != null && hd.Definition.RaceId == def.RequiredRaceId);
                }

                if (valid && !string.IsNullOrEmpty(def.RequiredHeroDefinitionId))
                {
                    valid = heroDefinitions.Any(hd =>
                        hd.Definition != null &&
                        hd.Definition.Id == def.RequiredHeroDefinitionId);
                }

                return valid;
            })
            .ToList();
    }

    /// <summary>
    /// Checks if a hero definition ID corresponds to a given race ID.
    /// </summary>
    private bool IsHeroOfRace(string heroDefinitionId, string raceId)
    {
        if (_characterRepo.TryGetDefinition(heroDefinitionId, out var def))
        {
            return def.RaceId == raceId;
        }
        return false;
    }

    /// <summary>
    /// Marks a quest as completed and immediately awards its gold and XP rewards.
    /// </summary>
    private void CompleteQuest(ActiveQuest quest, QuestDefinition def, Guild guild)
    {
        quest.IsCompleted = true;

        guild.Gold += def.RewardGold;
        _progressionService.AddXpAndLevelUpIfNeeded(guild, def.RewardXP);

        _logger.LogInformation(
            "Guild {GuildId} completed quest {QuestId} ({Name}). Rewards: {Gold}g, {XP}xp",
            guild.Id, quest.Id, def.Name, def.RewardGold, def.RewardXP);
    }
}