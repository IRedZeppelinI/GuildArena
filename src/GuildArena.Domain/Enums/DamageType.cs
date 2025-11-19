namespace GuildArena.Domain.Enums;

/// <summary>
/// Defines the classification of damage, which determines the defense stat used for mitigation.
/// </summary>
public enum DamageType
{//Ver magic
    Martial, // Antigo Physical
    Mystic,  // Antigo Mental/Magic
    Divine,  // Antigo Holy
    Void,    // Antigo Dark
    Primal,  // Antigo Nature
    True     // Mantém-se (Dano Verdadeiro)
}