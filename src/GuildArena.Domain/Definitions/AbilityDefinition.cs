namespace GuildArena.Domain.Definitions;

public class AbilityDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }    
    public List<EffectDefinition> Effects { get; set; } = new();
}


