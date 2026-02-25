using MatchType = GuildArena.Domain.Enums.Matches.MatchType; //conflicto com System.IO.MatchType

namespace GuildArena.Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public MatchType Type { get; set; }

    // Quem participou? (1 Player + AI, ou 2 Players)
    public ICollection<MatchParticipant> Participants { get; set; } = new List<MatchParticipant>();
}
