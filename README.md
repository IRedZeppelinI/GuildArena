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
* **Essence Economy:** A "colored" resource system (Vigor, Mind, Shadow, etc.) requiring strategic hand management and trade-offs.
* **Dynamic Modifiers:** Robust system for Buffs, Debuffs, Shields (with scaling), and Status Effects (Stun, Silence, etc.).
* **Data-Driven:** Heroes, Abilities, and Races are defined in JSON, allowing for balancing updates without recompilation.

### Roadmap
* [ ] Guild Management & Recruitment.
* [ ] World Map Exploration (POI System).
* [ ] Dynamic Dungeons.
* [ ] Shop & Gold Economy.

## Testing

The solution maintains a comprehensive test suite covering Unit Tests (Core & Application Logic) and Data Integration Tests.