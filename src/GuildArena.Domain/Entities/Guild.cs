namespace GuildArena.Domain.Entities;

public class Guild
{
    public int Id { get; set; }

    // Ligação ao Identity (AspNetUsers)
    // O ID do Identity é uma string (GUID) por defeito.
    public required string ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public required string Name { get; set; } // Nome visível no jogo
    public int Gold { get; set; } = 0;

    // Estatísticas Globais
    public int Wins { get; set; }
    public int Losses { get; set; }

    //Progression
    public int Level { get; set; } = 1;
    public int CurrentXP { get; set; } = 0;

    // Coleções
    public ICollection<Hero> Heroes { get; set; } = new List<Hero>();
    public ICollection<MatchParticipant> MatchHistory { get; set; } = new List<MatchParticipant>();

    //props navegação
    /// <summary>
    /// The currently active dungeon run for the guild, if any.
    /// </summary>
    public ActiveDungeonRun? ActiveDungeonRun { get; set; }

    /// <summary>
    /// Historical records of completed dungeon runs.
    /// </summary>
    public ICollection<GuildDungeonRecord> DungeonRecords { get; set; } = new List<GuildDungeonRecord>();

    // QUESTS

    /// <summary>
    /// The UTC timestamp of the last time daily quests were granted to the guild.
    /// Null if never granted.
    /// </summary>
    public DateTime? LastDailyQuestGrantedAt { get; set; }

    /// <summary>
    /// The UTC timestamp of the last time the guild re-rolled a quest.
    /// Null if never re-rolled.
    /// </summary>
    public DateTime? LastQuestRerollAt { get; set; }

    /// <summary>
    /// The collection of currently active (not yet completed) quests for the guild.
    /// </summary>
    public ICollection<ActiveQuest> ActiveQuests { get; set; } = new List<ActiveQuest>();
}