namespace GuildArena.Domain.Entities;

public class Guild
{
    public int Id { get; set; }

    // Ligação ao Identity (AspNetUsers)
    // O ID do Identity é uma string (GUID) por defeito.
    public required string ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public required string Name { get; set; } // Nome visível no jogo
    public int Gold { get; set; } = 0;

    // Estatísticas Globais
    public int Wins { get; set; }
    public int Losses { get; set; }

    // Coleções
    public ICollection<Hero> Heroes { get; set; } = new List<Hero>();
    public ICollection<MatchParticipant> MatchHistory { get; set; } = new List<MatchParticipant>();
}