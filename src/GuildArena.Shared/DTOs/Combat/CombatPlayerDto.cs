using GuildArena.Domain.Enums.Combat;
using GuildArena.Domain.Enums.Resources;

namespace GuildArena.Shared.DTOs.Combat;

/// <summary>
/// Represents a participating player and their current resources.
/// </summary>
public class CombatPlayerDto
{
    public int PlayerId { get; set; }
    public required string Name { get; set; }
    public CombatPlayerType Type { get; set; }

    public int MaxTotalEssence { get; set; }
    public Dictionary<EssenceType, int> EssencePool { get; set; } = new();
}