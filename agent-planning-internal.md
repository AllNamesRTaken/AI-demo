# Planning Agent - Internal Notes

## Context Analysis

### Current State
- **Solution**: AI-demo.sln with 6 projects (Domain, Application, Infrastructure, Contracts, Server, AvaloniaApp)
- **Target Framework**: .NET 10.0
- **Current Architecture**: Full Clean Architecture with:
  - SignalR hub (AppHub) with Item CRUD operations
  - OIDC authentication via Authentik (local dev)
  - martinothamar/Mediator 3.0.1 CQRS with pipeline behaviors
  - Outbox pattern, idempotency, EF Core + PostgreSQL
  - Avalonia desktop client with CommunityToolkit.Mvvm

### Target Architecture
Transform into **multiplayer Flappy Bird** game:
- Replace Item CRUD with real-time game mechanics
- Server-authoritative game loop at 20 Hz
- Passphrase-based room join via SignalR groups
- In-memory room state + persisted leaderboard scores
- Avalonia canvas rendering for game display

## Key Design Decisions

### 1. Naming Convention
- **Project Prefix**: `AiDemo` (matching solution name)

### 2. Scope
Replace Item CRUD with:
- Game rooms (passphrase-based, in-memory)
- Server game loop (physics, collision, pipes)
- Leaderboard (persisted `GameScore` entity)
- Real-time game state broadcast via SignalR

### 3. Package Dependencies
**No new NuGet packages needed** — existing dependencies cover all requirements.
Key packages: Mediator 3.0.1, Avalonia 11.3.11, SignalR, MessagePack, FluentValidation, EF Core/Npgsql, Polly 8.5.2

### 4. OIDC Provider
- **Local development**: Authentik (Docker, port 9000)
- **Production**: Cloud OIDC provider
- Use `Oidc:Authority` and `Oidc:ClientId` config keys

### 5. Architecture Decisions (AD-1 through AD-11)
See `agent-action-internal.md` for full details. Key highlights:
- AD-9: Direct-path vs Mediator-path classification for hub methods
- AD-10: IGameBroadcaster abstraction (circular dependency fix)
- AD-11: PeriodicTimer for game loops

## Risk Assessment
- **Game physics tuning**: Start with documented constants, iterate
- **SignalR bandwidth at 20 Hz**: ~2KB/tick is manageable for small rooms
- **PeriodicTimer precision**: 50ms intervals may drift under load — acceptable for Flappy Bird pace
