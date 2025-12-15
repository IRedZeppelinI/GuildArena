namespace GuildArena.Domain.Enums.Modifiers;

public enum ModificationType
{
    FLAT,       // Adiciona um valor fixo (ex: +10 Attack)
    PERCENTAGE  // Adiciona uma percentagem (ex: +0.05 = +5% Attack)
}