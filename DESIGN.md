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

A lógica de combate segue o **Padrão Strategy** (Especialistas) e o **Padrão Orchestrator** (Orquestrador). O `CombatEngine` não realiza cálculos complexos; ele coordena serviços especializados.

### 3.1. Os Participantes (O "Quem")
* **`Combatant`:** A unidade no tabuleiro. Contém `CurrentHP`, `BaseStats`, `ActiveCooldowns` e `ActiveModifiers` (que podem conter Barreiras).
* **`CombatPlayer`:** Representa o controlador (Humano ou AI). Gere recursos globais como a `EssencePool` (Dicionário de Essences) e Modifiers globais.
* **`GameState`:** O objeto raiz que contém a lista de `Combatants`, `Players`, e o fluxo de turnos.

### 3.2. Subsistema de Targeting (`ITargetResolutionService`)
Responsável por identificar quem será afetado por uma habilidade.
* **Pipeline de Resolução:**
    1.  **Identificação:** Filtra candidatos baseados na Regra (Aliado, Inimigo, Self, All).
    2.  **Estado:** Filtra mortos/vivos conforme a regra (`CanTargetDead`).
    3.  **Visibilidade (Stealth):** Filtra alvos com modifiers `IsUntargetable` (apenas em seleção unitária).
    4.  **Seleção:** Aplica a estratégia definida (`Manual`, `LowestHP`, `Random`, `AoE`), usando desempate determinístico por ID.

### 3.3. Subsistema de Economia e Custos
A execução de habilidades exige recursos (Essence e HP). Este sistema está dividido em "Contabilista" e "Banco".

* **Cálculo de Custos (`ICostCalculationService`):**
    * Gera a "Fatura" (`FinalAbilityCosts`) antes da execução.
    * **Lógica:** `Custo Base` - `Descontos do Caster` (CostModification) + `Taxas do Alvo` (Wards de Essence/HP).
* **Gestão de Recursos (`IEssenceService`):**
    * **Validação (`HasEnoughEssence`):** Verifica se o jogador tem saldo para pagar a fatura.
    * **Consumo (`ConsumeEssence`):** Deduz os valores da `EssencePool` baseando-se na alocação específica enviada pela UI.
    * **Geração:** Gere a entrada de Essence no início do turno (Base + Modifiers de Geração).

### 3.4. Subsistema de Dano e Resolução (`IDamageResolutionService`)
Substitui a antiga lógica fragmentada. Centraliza todo o cálculo pós-mitigação.

* **Fluxo de Dano:**
    1.  **Mitigação (Handler):** O `DamageEffectHandler` calcula o dano bruto vs Defesa/Resistência Mágica.
    2.  **Resolução (Service):** O `IDamageResolutionService` recebe o dano mitigado.
        * **Modificadores:** Aplica Buffs (Caster) e Resistências Percentuais/Flat (Alvo).
        * **Barreiras:** Verifica modifiers com `BarrierProperties`. Se as Tags coincidirem (ex: Barreira de Fogo vs Ataque de Fogo), o dano é absorvido pelo escudo.
    3.  **Aplicação:** O resultado final é subtraído ao HP do alvo.
* **True Damage:** Ignora Defesa, Resistências e Barreiras.

### 3.5. Outros Serviços Especialistas
* **`IStatCalculationService`:** Calcula stats finais: `(Base + Flat) * (1 + Percent)`.
* **`ICooldownCalculationService`:** Calcula o cooldown final, aplicando modificadores (ex: "Haste").

---

## 4. Gestão de Turnos e Fluxo de Jogo

A gestão do fluxo de tempo é separada da execução de habilidades.

### 4.1. O Padrão "Carregar -> Modificar -> Guardar"
Como a API é *stateless*, todas as ações de jogo seguem este fluxo rigoroso no `Application Layer`:
1.  **Carregar:** O Handler obtém o `GameState` do Redis via Repositório.
2.  **Modificar:** O Handler invoca um Serviço de Domínio (`Core`) para alterar o estado em memória.
3.  **Guardar:** O Handler persiste o `GameState` alterado no Redis.

### 4.2. O Gestor de Turnos (`ITurnManagerService`)
Este serviço (`Core`) é responsável pela lógica de transição:
1.  **Tick de Fim de Turno:** Reduz a duração de `ActiveCooldowns` e `ActiveModifiers` do jogador atual.
2.  **Rotação (Round-Robin):** Identifica o próximo `CombatPlayer` na lista.
3.  **Novo Turno:** Atualiza o `CurrentPlayerId` e incrementa o `CurrentTurnNumber`.
4.  **Recursos:** Invoca o `IEssenceService` para gerar a Essence do novo jogador.