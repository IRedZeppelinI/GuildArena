namespace GuildArena.Domain.Enums;

// NOVO: Em GuildArena.Domain/Enums/DeliveryMethod.cs
public enum DeliveryMethod
{
    Melee,   // Escala com Attack
    Ranged,  // Escala com Agility
    Spell,   // Escala com Magic
    Passive  // Não escala, usa o BaseAmount (para Curses, Auras, etc.)
}


