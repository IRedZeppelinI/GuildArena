namespace GuildArena.Domain.Enums;


public enum DamageType
{
    Physical, // Usa StatType.Defense do alvo
    Magic,    // Usa StatType.Magic (MagicDefense) do alvo
    Mental,   // Usa StatType.Magic (MagicDefense) do alvo
    Holy,     // Usa StatType.Magic (MagicDefense) do alvo
    Dark,     // Usa StatType.Magic (MagicDefense) do alvo
    Nature,   // Usa StatType.Magic (MagicDefense) do alvo
    True      // Ignora todas as defesas
}