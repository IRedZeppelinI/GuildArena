namespace GuildArena.Domain.ValueObjects;

public class ActiveModifier
{
    public string DefinitionId { get; set; } // "MOD_POISON_WEAK"
    public int TurnsRemaining { get; set; } // Ex: 3

    // Opcional: Quem o aplicou? (para escalar o dano do veneno com a MAGIA do atacante)
    public int CasterId { get; set; }
}
