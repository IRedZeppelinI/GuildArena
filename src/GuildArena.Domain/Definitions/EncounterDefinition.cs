namespace GuildArena.Domain.Definitions;

/// <summary>
/// Defines a static PvE encounter configuration.
/// Acts as a blueprint for instantiating a combat session.
/// </summary>
public class EncounterDefinition
{
    public required string Id { get; set; } // Ex: "ENC_FOREST_01"
    public required string Name { get; set; } // Ex: "Bandit Ambush"

    /// <summary>
    /// Descriptive text shown to the player before combat starts.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// ID referencing the environment/background art asset.
    /// </summary>
    public string? BackgroundId { get; set; } // Ex: "BG_FOREST_CLEARING"

    /// <summary>
    /// A rating (e.g., 1-5 stars) indicating the difficulty of this encounter.
    /// </summary>
    public int DifficultyRating { get; set; }

    /// <summary>
    /// The list of enemies present in this encounter.
    /// </summary>
    public List<EncounterEnemy> Enemies { get; set; } = new();

    // ==========================================
    // NESTED CLASSES
    // ==========================================

    /// <summary>
    /// Defines a specific enemy unit within an encounter.
    /// </summary>
    public class EncounterEnemy
    {
        /// <summary>
        /// Reference to the CharacterDefinition (the mob stats/skills).
        /// </summary>
        public required string CharacterDefinitionId { get; set; }

        /// <summary>
        /// The specific level for this mob instance.
        /// </summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// The slot/position on the board (e.g., 1=Front, 2=Back).
        /// Important for tactical games.
        /// </summary>
        public int Position { get; set; }
    }
}