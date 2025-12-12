# [GuildArena] Documento de Design de Arquitetura

Este documento descreve a arquitetura de software, os padrões de design e as regras de negócio fundamentais do projeto GuildArena. O objetivo é manter um registo vivo de como o sistema funciona, facilitando a manutenção e a integração de novas funcionalidades.

## 1. Visão Geral do Produto

**GuildArena** é um jogo de estratégia tática por turnos, focado em combate competitivo (PvP) e cooperativo (PvE), inspirado em mecânicas de TCG (Magic: The Gathering) e jogos táticos de browser como *Naruto Arena*.

### Conceito Principal: "Hero Collection & Tactics"
* **Heróis Pré-definidos:** O jogador coleciona e desbloqueia **Heróis** cujas regras e definições base encontram-se em ficheiros JSON.
* **Definição vs. Instância:**
    * As regras do herói (Skills, Stats Base) são imutáveis.
    * A instância do jogador (`HeroCharacter`) guarda o progresso dinâmico (Nível, XP).
* **Loadouts e Customização:** À medida que evoluem, os heróis desbloqueiam slots de modificadores (o **Loadout**). Isto permite ao jogador equipar "Runas" ou "Traits" extra para especializar a build da personagem (ex: tank, glass cannon), até um limite definido pelo nível.
* **Gestão de Guild:** O jogador gere uma "roster" de combatentes e escolhe uma equipa ("Team") para levar para cada batalha.

---

## 2. Arquitetura Técnica

O projeto segue estritamente os princípios da **Clean Architecture** e **Domain-Driven Design (DDD)**, utilizando .NET 9.

### 2.1. Camadas
1.  **Apresentação (`GuildArena.Web`):**
    * Cliente **Blazor WebAssembly**.
    * Responsabilidade: Apenas UI e Input. Não contém lógica de jogo.
    * Comunicação: HTTP (REST) para comandos e SignalR (WebSockets) para atualizações de combate em tempo real.
2.  **Contratos Partilhados (`GuildArena.Shared`):**
    * Biblioteca de classes partilhada entre Cliente e Servidor.
    * Responsabilidade: Contém apenas DTOs (Requests/Responses) e Enums de transporte para garantir tipagem forte na comunicação API.
3.  **API & Aplicação (`GuildArena.Api` / `GuildArena.Application`):**
    * Atua como "Servidor Autoritário" (Anti-Cheat).
    * Padrão **CQRS** (Command Query Responsibility Segregation) com **MediatR**.
    * Orquestra o fluxo, mas delega a lógica de combate para o Core.
4.  **Core do Jogo (`GuildArena.Core`):**
    * Contém o "Cérebro" do combate (`CombatEngine`, Services, Actions).
    * Totalmente isolado de I/O (Bases de dados, HTTP).
5.  **Domínio (`GuildArena.Domain`):**
    * Entidades, ValueObjects, Enums e Interfaces (Contratos).
    * Não tem dependências de outros projetos.
6.  **Infraestrutura (`GuildArena.Infrastructure`):**
    * Implementação de Repositórios (SQL, Redis, JSON File System).

### 2.2. Estratégia de Dados

| Tipo de Dados | Tecnologia | Descrição |
| :--- | :--- | :--- |
| **Estático (Blueprints)** | **JSON** | Definições de Heróis, Habilidades, Raças e Modificadores. Carregados para memória (Cache Singleton) no arranque. |
| **Persistente (Meta)** | **PostgreSQL** | Contas de jogadores, inventários, progresso de heróis (XP, Loadouts), guildas. |
| **Volátil (Combate)** | **Redis** | O estado de uma batalha ativa (`GameState`). Otimizado para leitura/escrita rápida. |

---

## 3. O Motor de Combate (Core Mechanics)

O combate opera num padrão de **Action Queue (Fila de Ações)**, o que elimina a recursividade e garante um fluxo de eventos claro e sequencial.

### 3.1. Fluxo de Execução ("The Pipeline")
A execução ocorre por agendamento e processamento linear:

1.  **Intenção:** A API recebe um Request, converte num Comando e chama `CombatEngine.ExecuteAbility`.
2.  **Queueing:** O Engine cria uma `ExecuteAbilityAction` (a raiz) e coloca-a na `IActionQueue`.
3.  **Processamento (Loop):**
    * O Engine retira a próxima ação da fila.
    * A ação é executada (Validação -> Custos -> Efeitos).
    * **Triggers:** Se a ação gerar eventos (ex: Dano), o `TriggerProcessor` cria *novas* ações (ex: "Thorns Damage") e coloca-as no fim da fila para execução subsequente.
4.  **Resultado:** O Engine retorna uma lista de `CombatActionResult`, contendo o histórico narrativo de tudo o que aconteceu em cadeia.

### 3.2. Battle Logs e Feedback
Cada ação processada gera um `CombatActionResult` que contém:
* `IsSuccess`: Se a ação ocorreu ou falhou.
* `BattleLogEntries`: Uma lista de strings formatadas para o jogador.
* `Tags`: Metadados para a UI disparar animações (ex: "Critical", "Miss").

### 3.3. Entidades e Serviços Chave
* **`ExecuteAbilityAction`:** Encapsula toda a lógica de uma habilidade (Validação de Status, Pagamento de Essence/HP, Cooldowns, Hit Chance).
* **`IEffectHandler`:** Implementações especializadas para cada tipo de efeito (Dano, Cura, Buffs).
* **`TriggerProcessor`:** Ouve eventos e agenda reações. Filtra eventos globais usando `ValidateCondition` (ex: garantir que só reajo se for eu a levar dano).

---

## 4. Economia de Essence (Mana System)

O sistema de recursos é inspirado em *Magic: The Gathering*, exigindo gestão estratégica de "cores" de mana.

### 4.1. Tipos de Essence
Existem 5 tipos principais (`Vigor`, `Mind`, `Light`, `Shadow`, `Flux`) e um tipo genérico (`Neutral`).

### 4.2. Pagamento e Validação
* **Custos Híbridos:** Uma habilidade pode custar "2 Vigor + 2 Neutral".
* **Decisão do Jogador:** O Backend não adivinha. O Cliente envia a alocação exata (ex: *"Pago 2 Vigor e 2 Mind"*).
* **Validação:** O `CostCalculationService` verifica se o pagamento cobre a fatura. Essences coloridas podem pagar custos Neutros, mas não o inverso.

### 4.3. Mecânica de "Transmutação" (Troca)
* O jogador pode, durante o seu turno, trocar recursos para corrigir uma mão má.
* **Taxa:** 2 Essences quaisquer por 1 Essence à escolha.

### 4.4. Geração e Ritmo (Handicap)
Para equilibrar a vantagem do "Primeiro a Jogar" (First Turn Advantage):
* **Turno 1 (Player 1):** Recebe apenas **2 Essence** (Aleatórias).
* **Turno 1 (Player 2) e seguintes:** Recebem **4 Essence** por turno.
* Modifiers (ex: "Mana Spring") podem alterar estes valores via `GenerateResourceHandler`.

---

## 5. Targeting e Visão Futura

O sistema de seleção de alvos (`ITargetResolutionService`) resolve quem vai sofrer os efeitos.

### 5.1. Estado Atual
Suporta regras baseadas em:
* **Relação:** Self, Ally, Enemy.
* **Quantidade:** Single Target, Multi-Target, AoE (All).
* **Estratégia Auto:** Random, LowestHP, HighestHP (para AI ou Triggers).

### 5.2. Visão Futura (Race & Traits)
Planeia-se expandir o sistema de filtros para permitir sinergias raciais e temáticas.
* *Exemplo:* "Curar todos os Aliados que sejam `Human`."
* *Exemplo:* "Causar dano extra a `Undead`."
Isto será feito através de tags e propriedades na `TargetingRule`.

---

## 6. Modificadores e Status (Buffs/Debuffs)

O sistema de Modifiers é a espinha dorsal da complexidade do jogo.
* **Stacking:** Modifiers podem acumular ou fazer refresh de duração.
* **Barreiras (Shields):** Suportam scaling (ex: 50% de Magic) e podem bloquear tipos específicos de dano (ex: "Fire Shield").
* **Status Effects (CC):**
    * `Stun`: Bloqueia tudo.
    * `Silence`: Bloqueia Skills (permite Basic Attack).
    * `Disarm`: Bloqueia Basic Attack (permite Skills).
    * `Invulnerable` / `Untargetable`: Regras defensivas absolutas.