using GuildArena.Domain.ValueObjects.Dungeons;

namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines a static Dungeon configuration composed of multiple stages.
/// </summary>
public class DungeonDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int RequiredGuildLevel { get; set; } = 1;
    public CompletionRewards CompletionRewards { get; set; } = new();
    public List<DungeonStage> Stages { get; set; } = new();

    // ==========================================
    // NESTED CLASSES
    // ==========================================

    public class DungeonStage
    {
        public int StageIndex { get; set; }
        public string BackgroundId { get; set; } = "bg_default";
        public bool IsBossNode { get; set; }
        public StageRewards StageRewards { get; set; } = new();

        // Aqui usamos a nossa própria definição de Inimigo!
        public List<DungeonEnemy> Enemies { get; set; } = new();
    }

    public class DungeonEnemy
    {
        public required string CharacterDefinitionId { get; set; }
        public int Level { get; set; } = 1;
        public int Position { get; set; }
    }
}