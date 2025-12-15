namespace GuildArena.Domain.Enums.Combat;

///// <summary>
///// Defines the classification of damage, which determines the defense stat used for mitigation.
///// </summary>
//public enum DamageCategory
//{//Ver magic
//    Martial, // Antigo Physical
//    Mystic,  // Antigo Mental/Magic
//    Divine,  // Antigo Holy
//    Void,    // Antigo Dark
//    Primal,  // Antigo Nature
//    True     // Mantém-se (Dano Verdadeiro)
//}



/// <summary>
/// Defines the mechanical category of damage, determining which defensive stat is used.
/// Thematic types (Fire, Void, Martial) are now handled via Tags.
/// </summary>
public enum DamageCategory
{
    Physical, // Mitigado por Defense
    Magical,  // Mitigado por MagicDefense
    True      // Ignora mitigação
}