# GuildArena

A turn-based, tactical, browser-based strategy game focused on guild management and deep combat.

## About The Project

GuildArena is a web-based game inspired by turn-based tactical RPGs (like *Naruto Arena*), but with additional layers of strategic depth. Players will manage a guild, recruit a roster of characters, explore a world, venture into dungeons, and engage in complex tactical combat.

The goal is to create a robust "web-native" gaming experience with deep gameplay mechanics, built on a modern, scalable, and 100% .NET software architecture.

## Core Features (Vision)

* **Tactical Turn-Based Combat:** Arena battles (PvP and PvE) where tactics, summons, and team synergies are key.
* **Deep Gameplay System:** A data-driven combat system with stats (Attack, Defense, Magic...), modifiers, buffs/debuffs, and hundreds of unique abilities.
* **Guild Management:** Recruit, train, and evolve a roster of characters.
* **Risk and Reward:** Characters can evolve but also face the risk of permadeath, making every decision meaningful.
* **World Exploration:** A "Points of Interest" (POI) style interactive world map.
* **Dynamic Dungeons:** "Choose Your Own Adventure" style dungeon exploration, complete with events, combat, and treasure.
* **Economy & Shop:** A gold and shop system for purchasing items or unlocking new characters.

## Architecture and Technology

This project is being built with a primary focus on maintainability, testability, and scalability, adhering to Clean Architecture principles.

* **Frontend: Blazor WebAssembly (Wasm)**
    * A pure "thin client" responsible only for the UI and sending user intentions.
    * Communicates with the backend via HTTP (REST) and WebSockets (SignalR).

* **Backend: ASP.NET Core Web API (.NET 8+)**
    * Acts as an Authoritative Server (Anti-Cheat) and game "Arbiter".
    * Exposes REST endpoints and a SignalR Hub (for PvP combat).

* **Architecture Pattern: CQRS (Command Query Responsibility Segregation)**
    * API logic is orchestrated using the MediatR pattern.
    * Clear separation of "Reads" (Queries) and "Writes" (Commands).

* **Game Logic: Data-Driven (Decoupled from the API)**
    * Pure business logic (e.g., `CombatEngine`, `DungeonService`) lives in a separate `Core` layer.
    * Abilities and characters are defined as data (e.g., JSON) and interpreted by Handlers (Strategy Pattern), allowing for easy balancing and expansion.

* **Persistence: SQL (PostgreSQL / MySQL)**
    * Managed via Entity Framework Core.
    * Handles all transactional data (players, guilds, inventory).

## Project Status

**Initial Development Phase**

This project is in its initial setup phase. The solution structure and architectural foundations are currently being defined.

## Getting Started (WIP)

Instructions on how to set up the development environment, launch the API (`GuildArena.Api`), and run the Blazor client (`GuildArena.Web`) will be added here.