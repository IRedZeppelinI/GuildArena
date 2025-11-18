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
* **Exemplos:** `CharacterDefinition`, `AbilityDefinition`, `ModifierDefinition`.
* **Tecnologia:** **Ficheiros JSON** (ex: `abilities.json`).
* **Implementação:** Lidos no arranque para uma **Cache Singleton (Dicionário O(1))**.
* **Interface:** `IModifierDefinitionRepository`.

### 2.3. Dados Voláteis (Estado do Combate Ativo)
* **O que são:** O estado em tempo real de um combate a decorrer (`GameState`).
* **Tecnologia:** **Redis (Chave-Valor)**.
* **Porquê:** Performance extrema (leitura/escrita em milissegundos) e suporte para reconexão (o estado vive no servidor, não no cliente).
* **Ciclo de Vida:** Criado no `StartCombat`, atualizado a cada ação, apagado no fim do combate.
* **Interface:** `ICombatStateRepository` (Implementação: `RedisCombatStateRepository`).

---

## 3. Arquitetura do Motor de Combate (Core)

A lógica de combate segue o **Padrão Strategy** (Especialistas) e o **Padrão Orchestrator** (Orquestrador).

### 3.1. Os Participantes (O "Quem")
* **`Combatant`:** A unidade no tabuleiro. Contém `CurrentHP`, `ActiveCooldowns`, `ActiveModifiers`.
* **`CombatPlayer`:** Representa o controlador (Humano ou AI). Gere recursos globais como `Essence`.
* **`GameState`:** O objeto raiz que contém a lista de `Combatants`, `Players`, e o `CurrentPlayerId` (de quem é a vez).

### 3.2. O Processo de Execução de Habilidade (`CombatEngine`)
O `CombatEngine` é o orquestrador principal.
1.  **Validação:** Verifica se o `Combatant` pode agir (Cooldowns, Stuns, Custos).
2.  **Targeting:** Resolve os alvos (`GetTargetsForRule`) baseado na `AbilityDefinition` e na seleção da UI.
3.  **Aplicação:** Itera pelos `Effects` e delega a execução aos `IEffectHandler`.
4.  **Cooldown:** Aplica o cooldown calculado via `ICooldownCalculationService`.

### 3.3. O Processo de Cálculo (Stats, Dano e Cooldowns)
Para garantir o **SRP**, os cálculos são delegados a serviços "Especialistas":

* **`IStatCalculationService`:** Calcula stats finais: `(Base + Flat) * (1 + Percent)`.
* **`IDamageModificationService`:** Aplica bónus/resistências baseados em **Tags** (ex: "+10% Fire Damage").
* **`ICooldownCalculationService` [NOVO]:** Calcula o cooldown final de uma habilidade, aplicando modificadores (ex: "Haste: -1 turno em habilidades Nature").

---

## 4. Gestão de Turnos e Fluxo de Jogo

A gestão do fluxo de tempo é separada da execução de habilidades.

### 4.1. O Padrão "Carregar -> Modificar -> Guardar"
Como a API é *stateless*, todas as ações de jogo (ex: `EndTurn`) seguem este fluxo rigoroso no `Application Layer`:
1.  **Carregar:** O Handler obtém o `GameState` do Redis (`ICombatStateRepository.GetAsync`).
2.  **Modificar:** O Handler invoca um Serviço de Domínio (`Core`) para alterar o estado em memória.
3.  **Guardar:** O Handler persiste o `GameState` alterado no Redis (`SaveAsync`).

### 4.2. O Gestor de Turnos (`ITurnManagerService`)
Este serviço (`Core`) é responsável pela lógica de transição de turno:
1.  **Tick de Fim de Turno:** Reduz a duração de `ActiveCooldowns` e `ActiveModifiers` dos combatentes do jogador atual.
2.  **Rotação (Round-Robin):** Identifica o próximo `CombatPlayer` na lista (suporta 1v1, FFA, PvE).
3.  **Novo Turno:** Atualiza o `CurrentPlayerId` e incrementa o `CurrentTurnNumber` se uma ronda completa passar.
4.  **Recursos:** (Futuro) Regenera recursos ou aplica efeitos de início de turno.