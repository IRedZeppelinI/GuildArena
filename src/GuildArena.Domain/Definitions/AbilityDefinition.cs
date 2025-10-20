namespace GuildArena.Domain.Definitions;

public class AbilityDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }    
    public List<EffectDefinition> Effects { get; set; }
}


