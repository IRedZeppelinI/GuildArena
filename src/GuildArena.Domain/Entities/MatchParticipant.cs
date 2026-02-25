namespace GuildArena.Domain.Entities;

public class MatchParticipant
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }
    public Match? Match { get; set; }

    // Se for NULL, significa que é a AI (Computador)
    public int? GuildId { get; set; }
    public Guild? Guild { get; set; }

    public bool IsWinner { get; set; }

    // Detalhe da equipa usada neste combate específico
    public ICollection<MatchHeroEntry> HeroesUsed { get; set; } = new List<MatchHeroEntry>();
}
