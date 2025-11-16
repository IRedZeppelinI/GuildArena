# [GuildArena] Documento de Design de Arquitetura

Este documento descreve a arquitetura de software de alto nível do projeto GuildArena, com foco na lógica de combate e na estrutura do `Domain`.

## 1. Visão Geral da Arquitetura

O projeto segue os princípios da **Clean Architecture** com uma separação clara entre Frontend (Cliente) e Backend (Servidor).

* **Frontend (`GuildArena.Web`):** Um cliente **Blazor WebAssembly (Wasm)**. É responsável apenas pela UI. É "burro" e não contém lógica de jogo.
* **Backend (`GuildArena.Api`):** Um servidor **ASP.NET Core Web API**. Atua como **Servidor Autoritário** (defesa anti-cheat) e "Árbitro" do jogo. Processa todas as ações.
* **Padrão de Lógica:** **CQRS (com MediatR)**. A API recebe "Comandos" (intenções de escrita) e "Queries" (pedidos de leitura) da UI.

---

## 2. Estratégia de Dados (SQL + JSON)

A persistência é dividida em dois tipos de dados:

### 2.1. Dados Dinâmicos (Estado do Jogador)

* **O que são:** O progresso do jogador (Nível, XP, Ouro, Heróis desbloqueados, Equipamento).
* **Tecnologia:** **Base de Dados SQL (Postgres/MySQL)**.
* **Porquê:** Necessidade de transações ACID (para compras na loja, recompensas de combate, etc.).
* **Entidades Principais:** `HeroCharacter`, `Player`, `Guild`.

### 2.2. Dados Estáticos (Regras do Jogo)

* **O que são:** Os "moldes" (blueprints) do jogo definidos pelo developer.
* **Exemplos:** `CharacterDefinition`, `AbilityDefinition`, `ModifierDefinition`.
* **Tecnologia:** **Ficheiros JSON** (ex: `abilities.json`, `modifiers.json`).
* **Implementação:** A camada `Infrastructure` será responsável por ler estes JSONs no arranque da API e guardá-los numa **Cache Singleton (Dicionário O(1))**.
* **Interface:** `IModifierDefinitionRepository`, `IAbilityDefinitionRepository` (definidas no `Domain`).

---

## 3. Arquitetura do Motor de Combate (Core)

A lógica de combate é construída usando o **Padrão Strategy** (Especialistas) e o **Padrão Orchestrator** (Orquestrador).

### 3.1. Os Participantes (O "Quem")

* **`CharacterDefinition` (Molde):** Dados estáticos (JSON). Define os *stats* base, *skills* e `Tags` de um tipo de Herói ou Mob.
* **`HeroCharacter` (Entidade):** Dados dinâmicos (SQL). Representa o Herói *específico* de um jogador (com Nível, XP, Perks).
* **`Combatant` (Estado em Memória):** A classe temporária usada *dentro* do combate. É "construída" no início da batalha a partir dos `HeroCharacter` (para jogadores) ou `CharacterDefinition` (para Mobs). Contém o `CurrentHP`, `MaxHP`, `OwnerId`, `BaseStats` (calculados de Nível+Equipamento) e `ActiveModifiers`.

### 3.2. O Processo de Targeting (A "Ação")

A execução de uma habilidade é um *pipeline* complexo que lida com múltiplos alvos (AoE, mistos).

1.  **Definição (`AbilityDefinition`):**
    * `TargetingRules` (Lista): Define a "lista de compras" de alvos (ex: [1 Inimigo, 1 Aliado]).
    * `Effects` (Lista): Define as ações. Cada `EffectDefinition` tem um `TargetRuleId` que o "liga" a um item da "lista de compras".
2.  **UI (Blazor):**
    * Lê as `TargetingRules`.
    * Pede ao jogador para selecionar os alvos para cada regra.
    * Constrói um `AbilityTargets` (um `Dictionary<string, List<int>>`) que mapeia o `RuleId` (ex: "T_StunTarget") aos IDs dos alvos (ex: `[5]`).
3.  **Execução (`CombatEngine`):**
    * O `ICombatEngine.ExecuteAbility` recebe o `GameState`, `source`, `ability` e o `AbilityTargets` (o "mapa").
    * O motor itera pelos `Effects` da habilidade.
    * Para cada `Effect`, ele usa o `TargetRuleId` para encontrar os IDs dos alvos no "mapa" `AbilityTargets`.
    * Ele chama o `Handler` (Especialista) apropriado para esses alvos.

### 3.3. O Processo de Efeitos (A "Lógica")

O `CombatEngine` (Orquestrador) não tem lógica. Ele delega o trabalho a "Especialistas" (`IEffectHandler`).

* **`IEffectHandler` (Interface):** Um contrato para um especialista (ex: `DamageEffectHandler`, `ApplyModifierHandler`).
* **Injeção:** O `CombatEngine` recebe um `IEnumerable<IEffectHandler>` e organiza-os num Dicionário O(1).
* **Fluxo:** O motor lê o `EffectType` (ex: `DAMAGE`) e chama o *handler* registado para esse tipo.

### 3.4. O Processo de Cálculo (Stats e Dano)

Este é o "caminho crítico" (hot path) e é dividido em duas fases:

**Fase 1: Cálculo de Stats (O `StatCalculationService`)**
* **Responsabilidade:** Calcular o *stat* final de um `Combatant` (ex: `Attack`).
* **Fórmula:** `(Base + Flat) * (1 + Percent)`
* **Lógica:**
    1.  Lê o `BaseStats` (nu) do `Combatant`.
    2.  Injeta o `IModifierDefinitionRepository` (para aceder à cache de JSONs).
    3.  Itera pela lista `ActiveModifiers` do `Combatant`.
    4.  Lê as `StatModifications` de cada *modifier* (ex: `+10 Attack [FLAT]`).
    5.  Calcula e devolve o *stat* final.

**Fase 2: Cálculo de Dano (O `DamageEffectHandler`)**
* **Responsabilidade:** Calcular o dano final de um efeito.
* **Lógica:**
    1.  **Ataque:** Lê o `DeliveryMethod` (ex: `Melee`) e chama o `IStatCalculationService` para obter o *stat* de ataque final (ex: `GetStatValue(source, StatType.Attack)`).
    2.  **Dano Bruto:** Calcula `(Stat * ScalingFactor) + BaseAmount`.
    3.  **Defesa:** Lê o `DamageType` (ex: `Holy`) e chama o `IStatCalculationService` para obter o *stat* de defesa final (ex: `GetStatValue(target, StatType.MagicDefense)`).
    4.  **Dano Mitigado:** `Dano Bruto - Defesa`.
    5.  **Bónus (Tags):** Itera pelos `ActiveModifiers` do *atacante* (para bónus de `Tags` ex: "+10% Holy") e do *alvo* (para resistências ex: "-20% Holy") e ajusta o `Dano Mitigado`.
    6.  **Aplicação:** Aplica o dano final ao `CurrentHP` do alvo.