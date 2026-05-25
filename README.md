# GuildArena

**GuildArena** is a browser-based tactical strategy game focused on deep combat (PvP & PvE), resource management, and hero collection.

Inspired by the tactical depth of *Naruto Arena* and *Magic: The Gathering*, it is built on a scalable, 100% .NET 9 architecture using Clean Architecture principles.

## Tech Stack

* **Frontend:** Blazor WebAssembly (Wasm)
* **Backend:** ASP.NET Core Web API (CQRS + MediatR)
* **Real-time:** SignalR (WebSockets)
* **Data:** Redis (Volatile Combat State), PostgreSQL (Persistence), JSON (Static Definitions)

## Architecture

The solution enforces strict separation of concerns to ensure testability and maintainability.

| Project | Responsibility |
| :--- | :--- |
| **GuildArena.Web** | Pure UI "Thin Client". Handles Input and Rendering. |
| **GuildArena.Shared** | Shared DTOs (Contracts) and Enums between Client and API. |
| **GuildArena.Api** | Authoritative Server. Orchestrates requests via CQRS. |
| **GuildArena.Core** | **Pure Game Logic.** Isolated from I/O. Contains the Combat Engine. |
| **GuildArena.Domain** | Entities, ValueObjects, and Business Rules. |
| **GuildArena.Infrastructure** | Implementation of Repositories (Redis, SQL, Files). |

> **For deep technical details on the Action Queue system, execution pipeline, and data strategy, please refer to [DESIGN.md](./DESIGN.md).**

## Key Features

### Combat Mechanics (Core)
* **Action Queue System:** A deterministic engine that processes actions, reactions, and triggers sequentially, enabling complex interactions without recursion issues.
* **Advanced Modifier Engine:** Robust system for Buffs, Debuffs, Shields (with stat scaling), Evasion/Accuracy, Armor Penetration, and Conditional Crits.
* **Observer Triggers & Death Lifecycle:** Modifiers can react to events happening to allies/enemies. A dedicated `DeathService` handles complex state cleanup (e.g., removing linked buffs when a caster dies).
* **Essence Economy:** A "colored" resource system (Vigor, Mind, Shadow, Flux, Light) requiring strategic hand management and trade-offs.
* **Data-Driven:** Heroes, Abilities, and Races are defined in JSON, allowing for balancing updates without recompilation.
* **AI Orchestrator:** A background service (`IAiTurnOrchestrator`) that manages AI turns asynchronously, utilizing an `IAiBehavior` strategy to evaluate valid targets and costs without cheating.

### Persistence & Meta-Game
* **Entity Framework Core & Identity:** Secure user authentication using ASP.NET Core Identity API Endpoints with **HTTP-Only Cookies** (zero-trust frontend architecture preventing XSS attacks).
* **Custom Claims Factory:** The player's `GuildId` is injected directly into the encrypted auth cookie via a custom `UserClaimsPrincipalFactory`, eliminating redundant database queries during normal site navigation.
* **Match & Roster History:** Relational tracking of player progression (XP, Level, Wins/Losses) and dynamic Roster management.

### Frontend Architecture (BFF Pattern & Predictor)
* **Backend-Driven UI:** The Blazor WebAssembly client acts as a "Thin Client". Complex validations (Targeting, Affordability, Taunt, Stealth) are pre-calculated by the API (`CombatStateMapper`) and sent via DTOs.
* **Predictor Pattern (Dynamic Tooltips):** The backend utilizes an `IEffectTooltipService` to pre-calculate ability math (Damage/Heals) and extract modifier lore based on the hero's *current* active buffs. The UI merely renders these predicted values, guaranteeing the frontend never duplicates combat formulas.
* **Real-Time Sync:** WebSockets (SignalR) push state updates and narrative `BattleLogs` to the client instantly.
* **Dynamic Asset Resolution:** A dedicated `AssetService` dynamically fetches portraits, ability icons, and backgrounds from Azure Blob Storage using naming conventions based on Definition IDs.

### Roadmap
* [x] Core Combat Engine & Unit Tests.
* [x] Relational Database Schema (EF Core) & Secure Identity (HTTP-Only Cookies).
* [x] SignalR Integration for Real-Time feedback.
* [x] Blazor WebAssembly Frontend (Combat Arena UI & State Machine).
* [x] Dynamic Meta-Game UI (Lobby, Roster, and Real-time Mathematical Tooltips).
* [x] Basic AI for PvE encounters (RandomBehavior & Orchestrator).
* [ ] Smart AI (Tactical evaluation algorithms).
* [ ] Hero Tavern (Shop), Gold Economy & Unlock Conditions.
* [ ] Dungeon Mode (Sequential PvE battles with persistent HP).

## Testing

The solution maintains a comprehensive test suite covering Unit Tests (Core & Application Logic) and Data Integration Tests.