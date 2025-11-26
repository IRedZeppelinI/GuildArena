namespace GuildArena.Domain.Enums;

public enum StatusEffectType
{
    None = 0,

    // --- Crowd Control (CC) ---
    Stun,           // Não pode agir (passa o turno)
    Silence,        // Não pode usar Skills (apenas Basic Attack)
    Disarm,         // Não pode usar Basic Attack (apenas Skills)    

    // --- Defensive States ---
    Invulnerable,   // Imune a todo o dano e efeitos negativos
    Untargetable,   // Não pode ser selecionado como alvo por inimigos
    Stealth,        // Untargetable + (geralmente) quebra ao causar dano

    // --- Agro / Control ---
    Taunted,        // Forçado a atacar a fonte do Taunt
    Charmed,        // Ataca  aliado 
    Feared          // Foge da fonte (movimento forçado) ou perde turno
}