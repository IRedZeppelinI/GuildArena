using GuildArena.Application.Abstractions;
using GuildArena.Application.Services;
using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Abstractions.Repositories;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums.Quests;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GuildArena.Application.UnitTests.Services
{
    public class QuestServiceTests
    {
        private readonly IQuestDefinitionRepository _questRepo;
        private readonly ICharacterDefinitionRepository _characterRepo;
        private readonly IGuildProgressionService _progressionService;
        private readonly IRandomProvider _random;
        private readonly ILogger<QuestService> _logger;
        private readonly QuestService _sut;

        public QuestServiceTests()
        {
            _questRepo = Substitute.For<IQuestDefinitionRepository>();
            _characterRepo = Substitute.For<ICharacterDefinitionRepository>();
            _progressionService = Substitute.For<IGuildProgressionService>();
            _random = Substitute.For<IRandomProvider>();
            _logger = Substitute.For<ILogger<QuestService>>();
            _sut = new QuestService(_questRepo, _characterRepo, _progressionService, _random, _logger);
        }

        // ─── Factory helpers ───────────────────────────────────────

        private static Guild CreateGuild(int id,
            string name = "TestGuild",
            int gold = 0,
            int level = 1,
            int xp = 0)
        {
            return new Guild
            {
                Id = id,
                ApplicationUserId = $"user_{id}",
                Name = name,
                Gold = gold,
                Level = level,
                CurrentXP = xp,
                Heroes = new List<Hero>(),
                ActiveQuests = new List<ActiveQuest>(),
                MatchHistory = new List<MatchParticipant>(),
            };
        }

        private static QuestDefinition CreateQuestDef(
            string id,
            QuestRequirementType requirementType,
            int targetValue = 1,
            string? requiredRaceId = null,
            string? requiredHeroDefId = null,
            int rewardGold = 0,
            int rewardXP = 0)
        {
            return new QuestDefinition
            {
                Id = id,
                Name = $"Quest {id}",
                Description = "Test",
                RequirementType = requirementType,
                TargetValue = targetValue,
                RequiredRaceId = requiredRaceId,
                RequiredHeroDefinitionId = requiredHeroDefId,
                RewardGold = rewardGold,
                RewardXP = rewardXP,
            };
        }

        private void SetupAllQuestDefs(params QuestDefinition[] defs)
        {
            var dict = defs.ToDictionary(d => d.Id, d => d);
            _questRepo.GetAllDefinitions().Returns(dict);
        }

        private void SetupCharacterDef(string characterId, string raceId)
        {
            _characterRepo.TryGetDefinition(characterId, out Arg.Any<CharacterDefinition>())
                .Returns(ci =>
                {
                    ci[1] = new CharacterDefinition
                    {
                        Id = characterId,
                        Name = "TestChar",
                        RaceId = raceId,
                        BaseStats = new Domain.ValueObjects.Stats.BaseStats(),
                        StatsGrowthPerLevel = new Domain.ValueObjects.Stats.BaseStats(),
                    };
                    return true;
                });
        }

        // ─── GrantDailyQuestsIfNeededAsync tests ───────────────────

        [Fact]
        public async Task GrantDailyQuestsIfNeededAsync_GivenAlreadyGrantedToday_DoesNothing()
        {
            var guild = CreateGuild(1);
            guild.LastDailyQuestGrantedAt = DateTime.UtcNow.Date;

            await _sut.GrantDailyQuestsIfNeededAsync(guild);

            guild.ActiveQuests.Count.ShouldBe(0);
            _questRepo.DidNotReceive().GetAllDefinitions();
        }

        [Fact]
        public async Task GrantDailyQuestsIfNeededAsync_WhenNoSlotsAvailable_SetsLastGrantedAndReturns()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests = new List<ActiveQuest>
            {
                new() { Id = 1, IsCompleted = false },
                new() { Id = 2, IsCompleted = false },
                new() { Id = 3, IsCompleted = false },
            };
            guild.LastDailyQuestGrantedAt = null;

            await _sut.GrantDailyQuestsIfNeededAsync(guild);

            guild.ActiveQuests.Count.ShouldBe(3);
            guild.LastDailyQuestGrantedAt.ShouldNotBeNull();
            guild.LastDailyQuestGrantedAt!.Value.Date.ShouldBe(DateTime.UtcNow.Date);
            _questRepo.DidNotReceive().GetAllDefinitions();
        }

        [Theory]
        [InlineData(1, 0, 0)]  // missed 1 day, 0 slots free → grant 0
        [InlineData(2, 1, 1)]  // missed 2 days, 1 slot free  → grant 1 (capped)
        [InlineData(5, 3, 3)]  // missed 5 days, 3 slots free → grant 3 (capped)
        public async Task GrantDailyQuestsIfNeededAsync_RespectsMissedDaysAndAvailableSlots(
            int missedDays, int availableSlots, int expectedGranted)
        {
            // Arrange
            var guild = CreateGuild(1);
            guild.LastDailyQuestGrantedAt = DateTime.UtcNow.AddDays(-missedDays).Date;

            // Pre‑fill active quests so we have exactly (3 - availableSlots) already active
            int alreadyActive = 3 - availableSlots;
            for (int i = 0; i < alreadyActive; i++)
            {
                guild.ActiveQuests.Add(new ActiveQuest
                {
                    Id = i + 1,
                    QuestDefinitionId = $"EXISTING_{i}",
                    IsCompleted = false
                });
            }

            // Provide enough distinct definitions for the grants
            var defs = new List<QuestDefinition>();
            for (int i = 0; i < 4; i++)
                defs.Add(CreateQuestDef($"Q_NEW_{i}", QuestRequirementType.PlayMatch));
            SetupAllQuestDefs(defs.ToArray());

            // Make random pick indices 0,1,2,… so we always get a valid index
            _random.Next(Arg.Any<int>()).Returns(0);

            // Act
            await _sut.GrantDailyQuestsIfNeededAsync(guild);

            // Assert
            guild.ActiveQuests.Count.ShouldBe(alreadyActive + expectedGranted);
            guild.LastDailyQuestGrantedAt!.Value.Date.ShouldBe(DateTime.UtcNow.Date);

            // Ensure all newly added quests come from our pool
            var newDefIds = guild.ActiveQuests
                .Where(q => !q.QuestDefinitionId.StartsWith("EXISTING"))
                .Select(q => q.QuestDefinitionId)
                .ToList();
            newDefIds.Count.ShouldBe(expectedGranted);
            newDefIds.All(id => id.StartsWith("Q_NEW_")).ShouldBeTrue();
        }

        [Fact]
        public async Task GrantDailyQuestsIfNeededAsync_FiltersOutAlreadyActiveQuests()
        {
            var guild = CreateGuild(1);
            guild.LastDailyQuestGrantedAt = null;
            guild.ActiveQuests.Add(new ActiveQuest { Id = 10, QuestDefinitionId = "Q1", IsCompleted = false });
            var def1 = CreateQuestDef("Q1", QuestRequirementType.PlayMatch);
            var def2 = CreateQuestDef("Q2", QuestRequirementType.PlayMatch);
            SetupAllQuestDefs(def1, def2);
            _random.Next(Arg.Any<int>()).Returns(0);

            await _sut.GrantDailyQuestsIfNeededAsync(guild);

            // Only Q2 should be added (Q1 already active)
            guild.ActiveQuests.Count.ShouldBe(2);
            guild.ActiveQuests.Any(q => q.QuestDefinitionId == "Q2").ShouldBeTrue();
        }

        [Fact]
        public async Task GrantDailyQuestsIfNeededAsync_ExcludesQuestsNotMatchingGuildRaceOrHero()
        {
            var guild = CreateGuild(1);
            guild.LastDailyQuestGrantedAt = null;
            guild.Heroes = new List<Hero>
            {
                new() { Id = 100, CharacterDefinitionId = "HERO_HUMAN", GuildId = 1, CurrentLevel = 1 }
            };
            SetupCharacterDef("HERO_HUMAN", "RACE_HUMAN");

            var defHumanRace = CreateQuestDef("Q_RACE", QuestRequirementType.PlayWithRace, requiredRaceId: "RACE_HUMAN");
            var defElfRace = CreateQuestDef("Q_ELF", QuestRequirementType.PlayWithRace, requiredRaceId: "RACE_ELF");
            var defHumanHero = CreateQuestDef("Q_HERO", QuestRequirementType.PlayWithHero, requiredHeroDefId: "HERO_HUMAN");
            var defOtherHero = CreateQuestDef("Q_OTHER", QuestRequirementType.PlayWithHero, requiredHeroDefId: "HERO_OTHER");
            var defSimple = CreateQuestDef("Q_SIMPLE", QuestRequirementType.PlayMatch);
            SetupAllQuestDefs(defHumanRace, defElfRace, defHumanHero, defOtherHero, defSimple);
            _random.Next(Arg.Any<int>()).Returns(0, 1, 2, 3, 4);

            await _sut.GrantDailyQuestsIfNeededAsync(guild);

            // Should not contain elf or other hero quests (they don't match guild)
            guild.ActiveQuests.Select(q => q.QuestDefinitionId)
                .ShouldAllBe(id => id == "Q_RACE" || id == "Q_HERO" || id == "Q_SIMPLE");
        }

        // ─── RerollQuestAsync tests ──────────────────────────────

        [Fact]
        public async Task RerollQuestAsync_GivenQuestNotFound_ReturnsFailure()
        {
            var guild = CreateGuild(1);
            var result = await _sut.RerollQuestAsync(guild, 999);
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("Quests.NotFound");
        }

        [Fact]
        public async Task RerollQuestAsync_GivenCompletedQuest_ReturnsFailure()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, IsCompleted = true });
            var result = await _sut.RerollQuestAsync(guild, 1);
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("Quests.AlreadyCompleted");
        }

        [Fact]
        public async Task RerollQuestAsync_GivenAlreadyRerolledToday_ReturnsFailure()
        {
            var guild = CreateGuild(1);
            guild.LastQuestRerollAt = DateTime.UtcNow.Date;
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, IsCompleted = false });
            var result = await _sut.RerollQuestAsync(guild, 1);
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("Quests.RerollUsed");
        }

        [Fact]
        public async Task RerollQuestAsync_WhenNoValidCandidates_ReturnsFailure()
        {
            // Simulate a scenario where after removing the quest, the only definitions
            // left in the pool are already present on other active quests.
            var guild = CreateGuild(1);
            var toReroll = new ActiveQuest { Id = 5, QuestDefinitionId = "OLD", IsCompleted = false };
            var otherQuest = new ActiveQuest { Id = 6, QuestDefinitionId = "ONLY", IsCompleted = false };
            guild.ActiveQuests = new List<ActiveQuest> { toReroll, otherQuest };

            SetupAllQuestDefs(CreateQuestDef("ONLY", QuestRequirementType.PlayMatch));
            // After removing "OLD", the only other definition is "ONLY", which is already active on otherQuest.
            // candidatePool becomes empty.

            var result = await _sut.RerollQuestAsync(guild, 5);
            result.IsFailure.ShouldBeTrue();
            result.Error.Code.ShouldBe("Quests.NoValidPool");
        }

        [Fact]
        public async Task RerollQuestAsync_ReplacesQuestWithNewDefinitionAndResetsProgress()
        {
            var guild = CreateGuild(1);
            var existingQuest = new ActiveQuest
            {
                Id = 5,
                QuestDefinitionId = "OLD",
                IsCompleted = false,
                CurrentProgress = 3,
                GuildId = guild.Id
            };
            guild.ActiveQuests.Add(existingQuest);

            var oldDef = CreateQuestDef("OLD", QuestRequirementType.PlayMatch);
            var newDef = CreateQuestDef("NEW", QuestRequirementType.PlayMatch);
            SetupAllQuestDefs(oldDef, newDef);
            _random.Next(Arg.Any<int>()).Returns(0); // picks "NEW"

            var result = await _sut.RerollQuestAsync(guild, 5);

            result.IsSuccess.ShouldBeTrue();

            // A quest mantém-se na coleção, apenas foi atualizada
            guild.ActiveQuests.Count.ShouldBe(1);

            // Garantimos que mudou para NEW
            existingQuest.QuestDefinitionId.ShouldBe("NEW");
            existingQuest.CurrentProgress.ShouldBe(0);
            guild.LastQuestRerollAt!.Value.Date.ShouldBe(DateTime.UtcNow.Date);
        }

        // ─── ProcessMatchEndAsync tests ──────────────────────────

        [Fact]
        public async Task ProcessMatchEndAsync_GivenNoIncompleteQuests_ReturnsEarly()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, IsCompleted = true });
            await _sut.ProcessMatchEndAsync(guild, new Match(), false);
            _questRepo.DidNotReceive().GetAllDefinitions();
            _progressionService.DidNotReceive().AddXpAndLevelUpIfNeeded(Arg.Any<Guild>(), Arg.Any<int>());
        }

        [Fact]
        public async Task ProcessMatchEndAsync_WhenGuildNotInMatchParticipants_SkipsProgress()
        {
            var guild = CreateGuild(1);
            var quest = new ActiveQuest { Id = 1, QuestDefinitionId = "Q", IsCompleted = false };
            guild.ActiveQuests.Add(quest);
            var match = new Match
            {
                Participants = new List<MatchParticipant>
                {
                    new() { GuildId = 999 } // different guild
                }
            };
            SetupAllQuestDefs(CreateQuestDef("Q", QuestRequirementType.PlayMatch));
            await _sut.ProcessMatchEndAsync(guild, match, false);
            quest.CurrentProgress.ShouldBe(0);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_WinMatchQuest_OnlyProgressesIfWinner()
        {
            var guild = CreateGuild(1);
            var quest = new ActiveQuest { Id = 1, QuestDefinitionId = "WIN", IsCompleted = false };
            guild.ActiveQuests.Add(quest);
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO1" });

            SetupAllQuestDefs(CreateQuestDef("WIN", QuestRequirementType.WinMatch, targetValue: 1, rewardGold: 10, rewardXP: 20));

            // Loss => no progress
            await _sut.ProcessMatchEndAsync(guild, match, isWinner: false);
            quest.CurrentProgress.ShouldBe(0);
            quest.IsCompleted.ShouldBeFalse();

            // Win => progress to target, complete and reward
            await _sut.ProcessMatchEndAsync(guild, match, isWinner: true);
            quest.CurrentProgress.ShouldBe(1);
            quest.IsCompleted.ShouldBeTrue();
            guild.Gold.ShouldBe(10);
            _progressionService.Received(1).AddXpAndLevelUpIfNeeded(guild, 20);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_PlayMatchQuest_AlwaysProgresses()
        {
            var guild = CreateGuild(1);
            var quest = new ActiveQuest { Id = 1, QuestDefinitionId = "PLAY", IsCompleted = false };
            guild.ActiveQuests.Add(quest);
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO1" });
            SetupAllQuestDefs(CreateQuestDef("PLAY", QuestRequirementType.PlayMatch, targetValue: 2, rewardGold: 5));

            // First match (win)
            await _sut.ProcessMatchEndAsync(guild, match, isWinner: true);
            quest.CurrentProgress.ShouldBe(1);
            quest.IsCompleted.ShouldBeFalse();

            // Second match (loss)
            await _sut.ProcessMatchEndAsync(guild, match, isWinner: false);
            quest.CurrentProgress.ShouldBe(2);
            quest.IsCompleted.ShouldBeTrue();
            guild.Gold.ShouldBe(5);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_PlayWithRaceQuest_ProgressesIfRaceUsed()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, QuestDefinitionId = "RACE", IsCompleted = false });
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO_ORC" });
            SetupCharacterDef("HERO_ORC", "RACE_ORC");
            SetupAllQuestDefs(CreateQuestDef("RACE", QuestRequirementType.PlayWithRace,
                targetValue: 1, requiredRaceId: "RACE_ORC", rewardGold: 15));

            await _sut.ProcessMatchEndAsync(guild, match, isWinner: true);
            guild.ActiveQuests.First().CurrentProgress.ShouldBe(1);
            guild.ActiveQuests.First().IsCompleted.ShouldBeTrue();
            guild.Gold.ShouldBe(15);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_PlayWithRaceQuest_NoProgressIfRaceNotUsed()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, QuestDefinitionId = "RACE", IsCompleted = false });
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO_ELF" });
            SetupCharacterDef("HERO_ELF", "RACE_ELF");
            SetupAllQuestDefs(CreateQuestDef("RACE", QuestRequirementType.PlayWithRace, requiredRaceId: "RACE_ORC"));

            await _sut.ProcessMatchEndAsync(guild, match, isWinner: true);
            guild.ActiveQuests.First().CurrentProgress.ShouldBe(0);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_PlayWithHeroQuest_ProgressesIfHeroUsed()
        {
            var guild = CreateGuild(1);
            guild.ActiveQuests.Add(new ActiveQuest { Id = 1, QuestDefinitionId = "HERO", IsCompleted = false });
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO_GARRET" });
            SetupAllQuestDefs(CreateQuestDef("HERO", QuestRequirementType.PlayWithHero,
                targetValue: 1, requiredHeroDefId: "HERO_GARRET", rewardGold: 10));

            await _sut.ProcessMatchEndAsync(guild, match, isWinner: true);
            guild.ActiveQuests.First().CurrentProgress.ShouldBe(1);
            guild.ActiveQuests.First().IsCompleted.ShouldBeTrue();
            guild.Gold.ShouldBe(10);
        }

        [Fact]
        public async Task ProcessMatchEndAsync_MultipleQuests_OnlyRelevantOnesProgress()
        {
            var guild = CreateGuild(1);
            var winQuest = new ActiveQuest { Id = 1, QuestDefinitionId = "WIN", IsCompleted = false };
            var playQuest = new ActiveQuest { Id = 2, QuestDefinitionId = "PLAY", IsCompleted = false };
            var raceQuest = new ActiveQuest { Id = 3, QuestDefinitionId = "RACE", IsCompleted = false };
            guild.ActiveQuests.Add(winQuest);
            guild.ActiveQuests.Add(playQuest);
            guild.ActiveQuests.Add(raceQuest);
            var match = BuildMatchWithGuild(guild.Id, new List<string> { "HERO_ORC" });
            SetupCharacterDef("HERO_ORC", "RACE_ORC");
            SetupAllQuestDefs(
                CreateQuestDef("WIN", QuestRequirementType.WinMatch, targetValue: 1),
                CreateQuestDef("PLAY", QuestRequirementType.PlayMatch, targetValue: 1),
                CreateQuestDef("RACE", QuestRequirementType.PlayWithRace, requiredRaceId: "RACE_ORC")
            );

            // Lose but with correct race
            await _sut.ProcessMatchEndAsync(guild, match, isWinner: false);

            winQuest.CurrentProgress.ShouldBe(0);   // no win
            playQuest.CurrentProgress.ShouldBe(1);  // played
            raceQuest.CurrentProgress.ShouldBe(1);  // race matched
        }

        // ─── Private helpers ─────────────────────────────────────

        private static Match BuildMatchWithGuild(int guildId, List<string> heroDefIds)
        {
            var participant = new MatchParticipant
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                HeroesUsed = heroDefIds.Select(h => new MatchHeroEntry
                {
                    Id = Guid.NewGuid(),
                    HeroDefinitionId = h,
                    LevelSnapshot = 1
                }).ToList()
            };
            return new Match
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Participants = new List<MatchParticipant> { participant }
            };
        }
    }
}