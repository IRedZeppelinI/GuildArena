# Registo de Heróis e Habilidades

Este documento serve como referência central para o design dos personagens, as suas habilidades, custos e mecânicas.
Qualquer alteração de balanceamento deve ser registada aqui.

---

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

---

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

---

## Planeamento (Futuro)

### 3. Elysia (The Oracle)
*   **Raça:** Psylian (Channel melhorado: gera 2 essence)
*   **Afinidade:** Mind (Mana Manipulation / Heals)
*   **Conceito:** Healer e Bateria de Mana.

### 4. Vex (The Unstable)
*   **Raça:** Kymera (Thorns: Reflete dano melee)
*   **Afinidade:** Flux (Randomness / High Risk)
*   **Conceito:** Berserker que pune quem lhe toca.

### 5. Nyx (The Shadow)
*   **Raça:** Nethra (Blur: Chance de Evasão)
*   **Afinidade:** Shadow (Stealth / DoTs)
*   **Conceito:** Assassino esquivo.

### 6. Solas (The Just)
*   **Raça:** Aureon (Divine Shell: Resistência a Status)
*   **Afinidade:** Light (Buffs / Anti-CC)
*   **Conceito:** Paladino de suporte puro.