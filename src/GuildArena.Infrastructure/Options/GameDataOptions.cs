namespace GuildArena.Infrastructure.Options;

public class GameDataOptions
{
    public const string SectionName = "GameData";

    // Caminho absoluto base 
    public string AbsoluteRootPath { get; set; } = string.Empty;

   
    public string RootFolder { get; set; } = "Data";
    public string ModifiersFile { get; set; } = "modifiers.json";
    public string RacesFile { get; set; } = "races.json";
    public string CharactersFile { get; set; } = "heroes.json";
    public string AbilitiesFolder { get; set; } = "Abilities";
    //como Abilities tem varios ficheiros, só é definido o nome da pasta o repositorio vai correr os ficheriso todos
}