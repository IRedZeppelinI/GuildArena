namespace GuildArena.Infrastructure.Options;

public class GameDataOptions
{
    public const string SectionName = "GameData";

    public string AbsoluteRootPath { get; set; } = string.Empty;
    public string RootFolder { get; set; } = "Data";

    public string RacesFile { get; set; } = "races.json";

    public string ModifiersFolder { get; set; } = "Modifiers";
    public string CharactersFolder { get; set; } = "Characters";
    public string AbilitiesFolder { get; set; } = "Abilities";
    public string EncountersFolder { get; set; } = "Encounters";

    /// <summary>
    /// The sub-folder containing dungeon JSON files.
    /// </summary>
    public string DungeonsFolder { get; set; } = "Dungeons";
}