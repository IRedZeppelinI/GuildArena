namespace GuildArena.Domain.Enums.Modifiers;

public enum StatusEffectType
{
    None = 0,

    // --- Crowd Control (CC) ---
    Stun,           // Não pode agir (passa o turno)
    Silence,        // Não pode usar Skills (apenas Basic Attack)
    Disarm,         // Não pode usar Basic Attack (apenas Skills)    
    Blind,          // Reduz Hit Chance ou ativa condições de crítico

    // --- Targeting / Defensive States ---
    Invulnerable,   // Imune a todo o dano e efeitos negativos
    Untargetable,   // Não pode ser selecionado como alvo por inimigos
    Taunted,        // Forçado a atacar a fonte do Taunt
    Stealth,        // Untargetable + (geralmente) quebra ao causar dano

    // ---  / Control ---
    Charmed,        // Ataca  aliado     
}