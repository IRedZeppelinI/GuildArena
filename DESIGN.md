Peço desculpa pela mudança repentina para Inglês na resposta anterior, foi um lapso no "context switching".

Aqui tens o **DESIGN.md** totalmente atualizado e em Português.
Incluí todas as alterações arquiteturais que fizemos (Factory, Raças, Action Points, Evasão, Triggers) e garanti que o `DamageResolutionService` está devidamente referenciado com a nova lógica de `DamageCategory`.

Podes substituir o conteúdo do teu ficheiro atual por este:

---

# [GuildArena] Documento de Design de Arquitetura

Este documento descreve a arquitetura de software de alto nível do projeto GuildArena, com foco na lógica de combate, persistência de estado e na estrutura do `Domain`.

## 1. Visão Geral da Arquitetura

O projeto segue os princípios da **Clean Architecture** com uma separação clara entre Frontend (Cliente) e Backend (Servidor).

* **Frontend (`GuildArena.Web`):** Um cliente **Blazor WebAssembly (Wasm)**. É responsável apenas pela UI. É "burro" e não contém lógica de jogo.
* **Backend (`GuildArena.Api`):** Um servidor **ASP.NET Core Web API**. Atua como **Servidor Autoritário** (defesa anti-cheat) e "Árbitro" do jogo. Processa todas as ações.
* **Padrão de Lógica:** **CQRS (com MediatR)**. A API recebe "Comandos" (intenções de escrita, ex: `EndTurnCommand`) e "Queries" (pedidos de leitura, ex: `GetCombatStateQuery`) da UI.

---

## 2. Estratégia de Dados (SQL + JSON + Redis)

A persistência é dividida em três tipos de dados, otimizados para diferentes necessidades:

### 2.1. Dados Dinâmicos (Estado do Jogador)
* **O que são:** O progresso persistente do jogador (Nível, XP, Ouro, Heróis desbloqueados).
* **Tecnologia:** **Base de Dados SQL (Postgres/MySQL)**.
* **Porquê:** Necessidade de transações ACID e relacionamentos fortes.
* **Entidades Principais:** `HeroCharacter`, `Player`, `Guild`.

### 2.2. Dados Estáticos (Regras do Jogo)
* **O que são:** Os "moldes" (blueprints) do jogo definidos pelo developer.
* **Tecnologia:** **Ficheiros JSON** (carregados via `IOptions` e Repositórios na Infrastructure).
* **Implementação:** Lidos no arranque (`Warmup` no Program.cs) para uma **Cache Singleton (Dicionário O(1))**.
* **Definições Principais:**
    * `CharacterDefinition`: Stats base, crescimento, referência à Raça.
    * `RaceDefinition`: Bónus de stats e Traits raciais (modificadores).
    * `AbilityDefinition`: Regras de habilidades (Custos, Efeitos).
    * `ModifierDefinition`: Regras de buffs/curses (Stats, Triggers).

### 2.3. Dados Voláteis (Estado do Combate Ativo)
* **O que são:** O estado em tempo real de um combate a decorrer (`GameState`).
* **Tecnologia:** **Redis (Chave-Valor)**.
* **Porquê:** Performance extrema (leitura/escrita em milissegundos) e suporte para reconexão (o estado vive no servidor).
* **Interface:** `ICombatStateRepository` (Implementação: `RedisCombatStateRepository`).

---

## 3. Arquitetura do Motor de Combate (Core)

A lógica de combate segue o **Padrão Strategy** (Especialistas) e o **Padrão Orchestrator** (Orquestrador). O `CombatEngine` coordena serviços especializados.

### 3.1. Os Participantes (O "Quem")
* **`Combatant`:** A unidade no tabuleiro.
    * **Stats:** `BaseStats` unificados (incluindo `MaxHP` e `MaxActions`).
    * **Estado:** `CurrentHP`, `ActionsTakenThisTurn`.
    * **Slots:** `BasicAttack`, `GuardAbility`, `FocusAbility` e lista de `Abilities`.
    * **Modifiers:** Lista de `ActiveModifier` (que contêm `ActiveStatusEffects` em cache).
* **`CombatPlayer`:** Representa o controlador. Gere a `EssencePool` e Modifiers globais.

### 3.2. Instanciação (`ICombatantFactory`)
Responsável por converter dados persistentes em combatentes de batalha.
* **Localização:** `GuildArena.Core.Combat.Factories`.
* **Fluxo de Criação:**
    1.  Carrega `CharacterDefinition` e `RaceDefinition`.
    2.  **Cálculo de Stats:** Soma Base + Raça + (Crescimento * Nível).
    3.  **Snapshot de HP:** Calcula o `MaxHP` inicial baseado na Defesa Total (fórmula de constituição) e congela o valor.
    4.  **Skills e Traits:** Resolve IDs de habilidades e aplica modificadores passivos (Raciais e Perks).

### 3.3. Sistema de Controlo e Validação (`IStatusConditionService`)
Valida se um combatente pode agir antes de processar custos.
* **Lógica:** Baseada no enum `ActionStatusResult` (Allowed, Stunned, Silenced, Disarmed).
* **Regras Partilhadas (`StatusEffectRules`):** Extension methods no Domain que definem o comportamento de cada `StatusEffectType` (ex: Stun bloqueia tudo, Silence bloqueia Skills). Usado tanto pelo Backend (validação) como pelo Frontend (UI).
* **Economia de Ações:** O `CombatEngine` valida e consome `ActionPointCost` contra o stat `MaxActions`.

### 3.4. Sistema de Dano e Precisão
Substitui a lógica simples por um pipeline de combate tático.

* **Precisão (`IHitChanceService`):**
    * Calcula a probabilidade de acerto (0% a 100%) antes de aplicar efeitos.
    * **Fórmula:** Base + (Ataque/Magia do Caster) - (Agilidade/MDef do Alvo) + Delta de Nível.
    * **Cache de Evasão:** O `CombatEngine` garante que o teste de evasão é feito apenas uma vez por alvo por habilidade (consistência entre múltiplos efeitos).
* **Mitigação e Resolução (`IDamageResolutionService`):**
    * Recebe o dano bruto se o ataque acertar.
    * **Categorias (`DamageCategory`):** Physical (mitigado por Defense), Magical (mitigado por MagicDefense) e True (ignora defesa).
    * **Interações:** Aplica modificadores percentuais/flat e resolve absorção de Barreiras baseada em Tags.

### 3.5. Sistema de Recursos e Custos
* **`EssenceAmount`:** ValueObject que define par Tipo/Quantidade (usado para custos e ganhos).
* **`ICostCalculationService`:** Calcula a "Fatura Final" (Custo Base - Descontos + Taxas de Wards).
* **`IEssenceService`:** Gere a pool do jogador. Suporta `AddEssence` com lógica de tipos aleatórios (`EssenceType.Random`) e caps máximos.
* **Manipulação (`ManipulateEssenceHandler`):** Handler de efeito para gerar recursos (Channeling) ou destruir recursos (Manaburn).

### 3.6. Sistema de Eventos (`ITriggerProcessor`)
Permite reações automáticas a eventos de combate.
* **Funcionamento:** O `TriggerProcessor` itera sobre os modificadores ativos para encontrar gatilhos correspondentes (ex: `ON_RECEIVE_DAMAGE`, `ON_ABILITY_CAST`).
* **Snapshot:** Usa um `TriggerContext` imutável para capturar o estado do evento.
* **Integração:** Injetado nos pontos críticos (`TurnManager`, `CombatEngine`, `DamageEffectHandler`) para disparar habilidades internas (ex: Thorns, DoTs).

---

## 4. Gestão de Turnos (`ITurnManagerService`)

Serviço responsável pela transição de estado temporal.
1.  **Fim de Turno:** Dispara triggers `ON_TURN_END`, reduz cooldowns e durações de modifiers.
2.  **Rotação:** Identifica o próximo jogador.
3.  **Início de Turno:**
    * Reseta `ActionsTakenThisTurn` a 0.
    * Gera Essence para o novo jogador.
    * Dispara triggers `ON_TURN_START`.