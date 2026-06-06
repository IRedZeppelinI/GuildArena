using System;
using System.Collections.Generic;

namespace GuildArena.Domain.Entities;

/// <summary>
/// Represents an active dungeon run for a guild, tracking progress through stages and hero states.
/// </summary>
public class ActiveDungeonRun
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the owning guild.
    /// </summary>
    public int GuildId { get; set; }

    /// <summary>
    /// Navigation property to the guild.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// The static definition ID (e.g., "DUNGEON_SHADOW_MAW").
    /// </summary>
    public string DungeonDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index of the current stage the guild is on.
    /// </summary>
    public int CurrentStageIndex { get; set; }

    /// <summary>
    /// State snapshot for the heroes used in this run.
    /// </summary>
    public ICollection<DungeonHeroState> HeroesState { get; set; } = new List<DungeonHeroState>();
}