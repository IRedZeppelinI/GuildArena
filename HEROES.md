# Registo de Heróis e Habilidades

Este documento serve como referência central para o design dos personagens, as suas habilidades, custos e mecânicas.
Qualquer alteração de balanceamento deve ser registada aqui.

------------------------------------------------------------

## 1. Garret (The Slayer)
*   **Raça:** Human (+1 Action Point)
*   **Role:** Physical DPS / Burst
*   **Afinidade:** Vigor (Physical)
*   **Estado:** ✅ Implementado

### Trait: Slayer's Instinct
*   **Passivo:** Causa **+2 de Dano** com qualquer habilidade que tenha a tag `Physical`.

### Habilidades

**1. Precision Strike**
*   **Custo:** 0 Essence
*   **Tags:** `Physical`, `Melee`, `Vigor`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano físico moderado (Scale 1.1). Substitui o ataque básico convencional.

**2. Pommel Bash**
*   **Custo:** 1 Vigor + 1 Neutral
*   **Tags:** `Physical`, `Melee`, `CC`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano físico baixo e aplica **Stun** (impede ações) por 1 turno.

**3. Adrenaline**
*   **Custo:** 1 Neutral
*   **Tags:** `Buff`, `Vigor`
*   **Alvo:** Self
*   **Efeito:** Aumenta **Attack** (+20%) e **Agility** (+10%) por 2 turnos.

**4. Slayer's Mercy (Ultimate)**
*   **Custo:** 2 Vigor + 1 Neutral
*   **Tags:** `Physical`, `Melee`, `Ultimate`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa **True Damage** massivo (Ignora defesa e barreiras físicas).

------------------------------------------------------------

## 2. Korg (The Obsidian Guardian)
*   **Raça:** Valdrin (-3 Dano recebido de `Melee` e `Ranged`)
*   **Role:** Tank / Support / Control
*   **Afinidade:** Vigor (Earth) / Light (Protection)
*   **Estado:** ✅ Implementado

### Trait: Obsidian Resonance
*   **Reactivo:** Sempre que recebe dano, ganha um stack de **Hardened** (+10% Defense). Acumula até 5 vezes.

### Habilidades

**1. Stone Fist**
*   **Custo:** 0 Essence
*   **Tags:** `Physical`, `Melee`, `Vigor`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano físico (Scale 1.0).

**2. Trembling Strike**
*   **Custo:** 1 Vigor
*   **Tags:** `Physical`, `Ranged`, `Vigor`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano físico e aplica **Concussion** (-20% Agility).

**3. Guardian's Link**
*   **Custo:** 1 Vigor + 1 Light
*   **Tags:** `Spell`, `Light`
*   **Alvo:** 1 Aliado (Não pode ser usado em si mesmo)
*   **Efeito:** Aplica uma Barreira (`Stone Shield`) que escala com a **Defense** do Korg.

**4. Fortress Form (Ultimate)**
*   **Custo:** 2 Vigor + 1 Light
*   **Tags:** `Spell`, `Ultimate`
*   **Alvos:** Self + 1 Inimigo
*   **Efeito (Self):** Ganha uma Barreira Massiva (`Fortress`).
*   **Efeito (Inimigo):** Aplica **Taunt** (Inimigo fica obrigado a atacar o Korg).

------------------------------------------------------------

## 3. Elysia (The Oracle)
*   **Raça:** Psylian (Trait Racial: Channel melhorado - gera 2 essence)
*   **Role:** Healer / Utility
*   **Afinidade:** Mind (Control) / Light (Healing)
*   **Estado:** ✅ Implementado

### Trait: Blessing of Light
*   **Reactivo:** Sempre que a Elysia restaura HP a um alvo (seja Single Target ou AoE), esse alvo ganha também **+2 Defense** (ou +10% se formos por percentagem) por 2 turnos.
    *   *Nota:* Com o Ultimate, isto significa um buff de defesa à equipa inteira.

### Habilidades

**1. Psionic Bolt**
*   **Custo:** 0 Essence
*   **Cooldown:** 0 Turnos
*   **Tags:** `Spell`, `Ranged`, `Mind`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano mágico (Scale 1.0 Magic). Ataca a **Magic Defense**.

**2. Mind Shock**
*   **Custo:** 1 Mind + 1 Neutral
*   **Cooldown:** 3 Turnos
*   **Tags:** `Spell`, `Ranged`, `Mind`, `CC`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano mágico leve (Scale 0.5 Magic) e aplica **Silence** por 1 turno.

**3. Mending Light**
*   **Custo:** 1 Mind + 1 Light
*   **Cooldown:** 0 Turnos (Spammable, limitado apenas pela Essence)
*   **Tags:** `Spell`, `Light`, `Heal`
*   **Alvo:** 1 Aliado
*   **Efeito:** Restaura HP (Scale 1.5 Magic). Aplica automaticamente o Trait (Defense Buff).

**4. Astral Convergence (Ultimate)**
*   **Custo:** 2 Mind + 2 Light
*   **Cooldown:** 5 Turnos
*   **Tags:** `Spell`, `Ultimate`, `Light`, `Heal`
*   **Alvo:** All Allies (AoE)
*   **Efeito:** Restaura HP a todos os aliados (Scale 1.2 Magic). Aplica o Trait (Defense Buff) a todos.


------------------------------------------------------------

## 4. Vex (The Unstable)
*   **Raça:** Kymera (Trait Racial: **Flux Conductor**)
    *   *Efeito:* No início do combate, gera automaticamente **1 Flux Essence**.
    *   *Mecânica:* Trigger `ON_COMBAT_START`.
*   **Role:** Berserker / Anti-Melee
*   **Afinidade:** Vigor (HP Sacrifice) / Flux (Chaos)
*   **Estado:** ✅ Implementado

### Trait: Spiked Carapace
*   **Reactivo:** Sempre que recebe um ataque **Melee**, reflete dano ao atacante.
*   **Fórmula:** 2 + (0.25 * Attack). (Ex: Com 14 Attack -> 5.5 Dano).

### Habilidades

**1. Reckless Strike**
*   **Custo:** 0 Essence | **10 HP**
*   **Tags:** `Physical`, `Melee`, `Vigor`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano físico elevado (Scale 1.4 Attack).

**2. Chaos Barrage**
*   **Custo:** 1 Flux
*   **Tags:** `Spell`, `Magical`, `Flux`
*   **Alvo:** 2 Inimigos Aleatórios (`Strategy: Random`)
*   **Efeito:** Causa dano mágico médio (Scale 0.8 Magic).

**3. Blood Fuel**
*   **Custo:** **15 HP**
*   **Cooldown:** 3 Turnos
*   **Tags:** `Utility`, `Buff`
*   **Alvo:** Self
*   **Efeito:** 
    1. Gera **2 Vigor Essence** imediatamente.
    2. Aplica **Blood Rush** (+30% Attack) por 1 turno.

**4. Decimate (Ultimate)**
*   **Custo:** 2 Vigor + 1 Flux
*   **Cooldown:** 5 Turnos
*   **Tags:** `Physical`, `Melee`, `Ultimate`
*   **Alvo:** 1 Inimigo
*   **Efeito:**
    1. Aplica buff **Predator Instinct** (Trigger: `ON_DEATH` -> Heal Self) no Vex.
    2. Causa **True Damage** massivo (Scale 1.5 Attack + 30 Base).
    *   *Combo:* Se o dano matar o alvo, o trigger dispara e o Vex recupera vida.


------------------------------------------------------------


## 5. Nyx (The Shadow)
*   **Raça:** Nethra (Trait Racial: **Blur**)
    *   *Efeito:* Possui **+15% Evasão** contra ataques físicos.
    *   *Mecânica:* `EvasionModifications` no `HitChanceService`.
*   **Role:** Assassin / Evasion Tank
*   **Afinidade:** Shadow (Void)
*   **Estado:** *A Implementar*

### Trait: Phase Shift
*   **Reactivo:** Sempre que a Nyx se esquiva (Evade/Miss) de um ataque, ganha **+10% Agility**  por 2 turnos. Acumula.
*   **Mecânica:** Trigger `ON_EVADE`.

### Habilidades

**1. Phantom Cut**
*   **Custo:** 1 Shadow
*   **Tags:** `Melee`, `Physical`, `Shadow`
*   **Alvo:** 1 Inimigo
*   **Efeito:** Dano Físico. Ignora **50% da Defesa** do alvo (Penetration).

**2. Shroud of Night**
*   **Custo:** 1 Shadow
*   **Cooldown:** 3 Turnos
*   **Alvo:** Self
*   **Efeito:** 
    1. Aplica **Stealth** (Untargetable).
    2. Aplica **+50% Evasão** (Para sobreviver a AoE).
    *   **Duração:** 2 Turnos.

**3. Withering Gaze**
*   **Custo:** 1 Shadow
*   **Cooldown:** 2 Turnos
*   **Alvo:** 1 Inimigo
*   **Efeito:** Causa dano mágico leve e aplica **Blind** (-20% Hit Chance) por 2 turnos.

**4. Void Singularity (Ultimate)**
*   **Custo:** 2 Shadow + 1 Flux
*   **Cooldown:** 5 Turnos
*   **Tags:** `Spell`, `Ultimate`, `Shadow`
*   **Alvo:** 1 Inimigo
*   **Efeito:** 
    1. Causa dano massivo.
    2. **Condicional:** Se o alvo tiver **Blind**, o ataque torna-se **Impossible to Evade** e causa **+50% de Dano**.

------------------------------------------------------------

### 6. Solas (The Just)
*   **Raça:** Aureon (Divine Shell: Resistência a Status)
*   **Afinidade:** Light (Buffs / Anti-CC)
*   **Conceito:** Paladino de suporte puro.