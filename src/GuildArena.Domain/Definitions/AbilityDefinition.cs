namespace GuildArena.Domain.Definitions;

public class AbilityDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    //Temporario como int até implemenentacao de Essence
    public int EssenceCost { get; set; }  //TODO: ImplementarEssence
    public int HPCost { get; set; }
    public int BaseCooldown { get; set; }
    public List<EffectDefinition> Effects { get; set; } = new();
}


