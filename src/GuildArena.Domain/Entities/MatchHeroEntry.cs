namespace GuildArena.Domain.Entities;

public class MatchHeroEntry
{
    public Guid Id { get; set; }

    public Guid MatchParticipantId { get; set; }
    public MatchParticipant? MatchParticipant { get; set; }
    
    public required string HeroDefinitionId { get; set; }
    
    public int LevelSnapshot { get; set; }
}