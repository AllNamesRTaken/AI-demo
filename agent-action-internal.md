# Implementation Agent — Internal Context

## Goal
Transform the AI-demo application into a **multiplayer Flappy Bird game** where players:
- Authenticate via OIDC (existing mechanism — keep as-is)
- Create or join a **game room** using a shared passphrase
- See all players' birds simultaneously in real-time
- Compete until all players are eliminated; highest score wins

---

## Current Codebase Summary

| Layer | Key Files | Action |
|-------|-----------|--------|
| Domain | `Item` entity | Remove; add `GameScore` entity |
| Application | CRUD commands/queries for Items | Remove; add game commands |
| Infrastructure | EF Core DbContext, outbox, idempotency | Keep infra patterns; add `GameRoomService` (in-memory); add `GameScore` config |
| Contracts | `IAppHub`, `IAppHubClient`, Item DTOs | Remove; replace with `IGameHub`, `IGameHubClient`, game DTOs |
| Server | `AppHub`, rate limiting, OIDC auth | Keep auth/rate-limiting; replace `AppHub` with `GameHub` |
| Avalonia client | MainWindowViewModel, ItemViewModel, item views | Keep login/auth flow; replace main UI with Lobby + Game views |

**Keep unchanged:** OIDC authentication, SignalR connection setup, retry/reconnect policy, idempotency service infrastructure, outbox processor, rate limiting middleware, `ErrorViewModel`, health endpoints, token refresh endpoint.

---

## Architecture Decisions

### AD-1: Server-Authoritative Game Loop
**Decision:** The server runs the physics simulation and broadcasts game state to all room clients.

**Rationale:**
- All players see identical state (fairness, no cheating)
- Client is just a renderer — receive state, draw it
- At ~20 ticks/sec bandwidth is ~2KB/tick per room (very manageable)

**Tick rate:** 50 ms intervals (~20 Hz). Flappy Bird's slow pace makes 20 Hz feel smooth.

### AD-2: In-Memory Room Management (No DB for Room State)
**Decision:** Game rooms live in a `ConcurrentDictionary` managed by `GameRoomService` (singleton). Only final scores are persisted to DB.

**Rationale:** Rooms are ephemeral. No need for distributed state on a single server.

### AD-3: Passphrase-Based Room Join
**Decision:** Rooms are identified by a user-chosen passphrase (case-insensitive, trimmed). First player with a passphrase creates it; others join with the same passphrase.

### AD-4: SignalR Groups for Room Broadcast
**Decision:** Each room maps to a SignalR group named `room:{passphrase}` (lowercased). The game loop calls `Clients.Group(...)` to broadcast ticks.

### AD-5: Shared Pipes, Individual Birds
**Decision:** Pipe obstacles are generated server-side and are identical for all players in a room. Each player has their own bird with independent Y position, vertical velocity, and alive state.

### AD-6: Avalonia Canvas Rendering
**Decision:** The client uses a custom `GameCanvas` control that overrides `Render(DrawingContext)` on an Avalonia `Control`. A `DispatcherTimer` at 60 fps invalidates the canvas, which redraws based on the latest `GameStateDto` received via SignalR.

No third-party game engine. Pure Avalonia drawing APIs (`DrawingContext.DrawEllipse`, `DrawRectangle`, etc.).

### AD-7: Replace Hub Interfaces Entirely
**Decision:** Delete `IAppHub`/`IAppHubClient`. Create `IGameHub`/`IGameHubClient`. Rename `AppHub.cs` to `GameHub.cs`. Delete all Item CRUD.

### AD-8: Keep Infrastructure Patterns
**Decision:** Keep outbox, idempotency behavior, and EF Core patterns. Remove `Item` entity/table. Add `GameScore` entity/table for the leaderboard.

### AD-9: Direct-Path vs Mediator-Path Hub Methods
**Decision:** Hub methods are classified as either **direct-path** or **mediator-path**. Direct-path methods bypass the Mediator pipeline and call `IGameRoomService` directly from `GameHub`. Mediator-path methods go through `IMediator.Send()` with the full pipeline.

**Classification criteria — a hub method is direct-path when ALL of these are true:**
1. **Latency-critical** — player input that must reach the game loop within the current tick window (50ms)
2. **No persistence** — does not write to the database (all state is in-memory)
3. **No cross-cutting concerns needed** — no idempotency, no validation beyond trivial null checks, no outbox
4. **Idempotent by nature** — repeating the call has no harmful side-effects (e.g., enqueuing a second flap just means the bird flaps again)

**A hub method is mediator-path when ANY of these are true:**
1. **Persists data** — writes to DB (scores, outbox messages)
2. **Needs idempotency** — duplicate calls must return the same result (e.g., double-join on reconnect)
3. **Needs validation** — complex input validation via `FluentValidation`
4. **Triggers outbox** — reliable event delivery required
5. **Benefits from pipeline behaviors** — logging, metrics, or future middleware

**Current classification:**

| Hub Method | Path | Reason |
|------------|------|--------|
| `FlapAsync` | Direct | Hot-path input ~multiple/sec; no DB; repeating is harmless |
| `SetReadyAsync` | Direct | Simple boolean toggle; no DB; idempotent (ready→ready is no-op) |
| `CreateOrJoinRoomAsync` | Mediator | Needs idempotency (reconnect double-join); validation (passphrase rules) |
| `LeaveRoomAsync` | Mediator | Needs cleanup logic; may trigger outbox if room empties |
| `GetLeaderboardAsync` | Mediator | DB query; benefits from caching pipeline |

**Why this matters — overhead avoided on direct-path calls:**

The Mediator pipeline executes 3 behaviors per call (configured in `DependencyInjection.cs`):
1. **`LoggingBehavior`** — logs entry/exit + elapsed time. Each call: 2× `ILogger` calls + `Stopwatch` allocation. At 5 flaps/sec across 8 players = 40 calls/sec = 80 log entries/sec of useless noise.
2. **`ValidationBehavior`** — resolves `IValidator<T>` from DI, runs `ValidateAsync()`. For `FlapCommand` there is nothing to validate (no input besides userId), but the behavior still does DI resolution + empty validator list iteration.
3. **`IdempotencyBehavior`** — checks `IIdempotentCommand<T>`, queries the `IdempotencyRecords` table. For stateless inputs like flap this is a **database round-trip on every keypress** — completely wasteful.

Total overhead per Mediator call: ~1 DI scope creation + 3 pipeline hops + potential DB query + object allocations for the command record. For a 50ms game tick budget, this overhead is meaningful.

**Extensibility:** As the game evolves, new hub methods should be classified using the same criteria. Future examples that would be direct-path: `UseBoostAsync`, `ChangeLaneAsync`, `EmoteAsync`. Future mediator-path: `PurchaseItemAsync`, `ReportPlayerAsync`, `SaveReplayAsync`.

### AD-10: `IGameBroadcaster` Abstraction (Circular Dependency Fix)
**Decision:** Define `IGameBroadcaster` in `AiDemo.Application/Interfaces/`. Implement as `SignalRGameBroadcaster` in `AiDemo.Server/Services/` using `IHubContext<GameHub, IGameHubClient>`. `GameRoomService` in Infrastructure injects `IGameBroadcaster` (not `IHubContext` directly).

**Rationale:** `GameRoomService` lives in Infrastructure, which cannot reference the Server project (that would create a circular dependency: Infrastructure → Server → Infrastructure). The abstraction keeps Infrastructure independent of SignalR hub types.

```csharp
// AiDemo.Application/Interfaces/IGameBroadcaster.cs
public interface IGameBroadcaster
{
    Task BroadcastGameTickAsync(string roomGroup, GameStateDto state, CancellationToken ct = default);
    Task BroadcastRoomUpdatedAsync(string roomGroup, GameRoomDto room, CancellationToken ct = default);
    Task BroadcastGameEndedAsync(string roomGroup, GameResultDto result, CancellationToken ct = default);
    Task BroadcastCountdownAsync(string roomGroup, int secondsRemaining, CancellationToken ct = default);
}
```

### AD-11: `PeriodicTimer` for Game Loop
**Decision:** Use `PeriodicTimer` (async-native, .NET 6+) instead of `System.Timers.Timer` for the server game loop.

**Rationale:** `System.Timers.Timer` fires callbacks on thread-pool threads and can re-enter if a tick takes longer than the interval. `PeriodicTimer.WaitForNextTickAsync()` is sequential by design — the next tick doesn't start until the previous one completes — eliminating race conditions without explicit locking. It's also `async`-native, so awaiting `IGameBroadcaster` calls is natural.

```csharp
// In GameRoomService — one loop per active room
private async Task RunGameLoopAsync(GameRoom room, CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
    while (await timer.WaitForNextTickAsync(ct))
    {
        GamePhysicsEngine.Tick(room, 0.05f);
        await _broadcaster.BroadcastGameTickAsync(room.GroupName, room.ToStateDto(), ct);
    }
}
```

---

## Game Mechanics

### Physics Constants (server-side, logical pixels/sec)
```
GRAVITY              = 1200f   // downward acceleration px/s²
FLAP_IMPULSE         = -420f   // upward velocity on flap px/s
PIPE_SPEED           = 150f    // pipes move left px/s
PIPE_GAP             = 160f    // vertical gap between top & bottom pipe
PIPE_WIDTH           = 60f
PIPE_SPAWN_INTERVAL  = 2.2f    // seconds between new pipe pairs
BIRD_X               = 80f     // fixed horizontal position for all birds
BIRD_RADIUS          = 14f     // collision circle radius
WORLD_WIDTH          = 800f    // logical world width
WORLD_HEIGHT         = 500f    // logical world height
MIN_PIPE_TOP         = 60f     // min height of top pipe
MAX_PIPE_TOP         = 280f    // max height of top pipe
```

### Room / Game State Machine
```
Waiting   → (all players ready)  → Countdown
Countdown → (3 sec elapsed)      → Running
Running   → (all birds dead)     → Ended
Ended     → (new round / leave)  → Waiting  (room reset)
```

### Server Game Loop (per 50 ms tick)
1. `dt = 0.05f`
2. For each alive bird: `velocity += GRAVITY * dt`, `y += velocity * dt`
3. Process pending flap queue for each player: `velocity = FLAP_IMPULSE`
4. Move all pipes: `x -= PIPE_SPEED * dt`
5. Remove pipes that have scrolled off-screen (x < -PIPE_WIDTH)
6. Spawn new pipe if `timeSinceLastPipe >= PIPE_SPAWN_INTERVAL`
7. Collision detection per alive bird:
   - Hit ceiling (y < 0) or floor (y > WORLD_HEIGHT) → dead
   - Hit any pipe rectangle → dead
   - On death: record score = number of pipes fully passed
8. Increment score for alive birds each tick (or count passed pipes)
9. If all birds dead → transition to `Ended`, persist `GameScore` rows
10. Broadcast `GameStateDto` to SignalR group

---

## New Files to Create

### AiDemo.Contracts
- `DTOs/Game/PlayerStateDto.cs` — bird position, velocity, alive, score, userId, displayName
- `DTOs/Game/PipeDto.cs` — x position, gapTopY
- `DTOs/Game/GameStateDto.cs` — tick number, players, pipes, gameStatus, countdown
- `DTOs/Game/GameRoomDto.cs` — roomId, passphrase, players, status, maxPlayers
- `DTOs/Game/GameResultDto.cs` — list of PlayerResultDto (rank, score, userId, displayName)
- `DTOs/Game/JoinRoomDto.cs` — passphrase, displayName
- `Hubs/IGameHub.cs` — `CreateOrJoinRoomAsync`, `LeaveRoomAsync`, `SetReadyAsync`, `FlapAsync`, `GetLeaderboardAsync`
- `Hubs/IGameHubClient.cs` — `OnRoomUpdated`, `OnGameStarted`, `OnCountdown`, `OnGameTick`, `OnGameEnded`, `OnPlayerJoined`, `OnPlayerLeft`, `OnError`

### AiDemo.Domain
- `Entities/GameScore.cs` — Id, RoomId, UserId, DisplayName, Score, AchievedAt
- **Delete:** `Entities/Item.cs`, `Events/ItemCreatedEvent.cs`

### AiDemo.Application
- `Commands/JoinRoom/JoinRoomCommand.cs` + `JoinRoomHandler.cs` — mediator-path (idempotency, validation)
- `Commands/LeaveRoom/LeaveRoomCommand.cs` + `LeaveRoomHandler.cs` — mediator-path (cleanup, outbox)
- `Queries/GetLeaderboard/GetLeaderboardQuery.cs` + `GetLeaderboardHandler.cs` — mediator-path (DB query)
- `Interfaces/IGameRoomService.cs` — contract for in-memory room management + direct-path methods
- `Interfaces/IGameBroadcaster.cs` — abstraction for SignalR broadcasting (circular dependency fix)
- ~~`Commands/Flap/`~~ — removed (direct-path per AD-9)
- ~~`Commands/SetReady/`~~ — removed (direct-path per AD-9)
- **Delete:** all `Commands/CreateItem`, `Commands/UpdateItem`, `Commands/DeleteItem`, `Queries/GetItems`, `Queries/GetItemById`

### AiDemo.Infrastructure
- `Services/GameRoomService.cs` — singleton, manages rooms + runs game loops via `PeriodicTimer`; injects `IGameBroadcaster`
- `Services/GamePhysicsEngine.cs` — static helpers: `ApplyGravity`, `CheckCollision`, `GeneratePipe`
- `Persistence/Configurations/GameScoreConfiguration.cs`
- New EF migration for `GameScores` table
- **Delete:** `Persistence/Configurations/ItemConfiguration.cs`
- **Modify:** `ApplicationDbContext.cs` — remove `Items`, add `GameScores`

### AiDemo.Server
- `Hubs/GameHub.cs` — implements `IGameHub`; direct-path methods call `IGameRoomService`; mediator-path methods use `IMediator`
- `Services/SignalRGameBroadcaster.cs` — implements `IGameBroadcaster` using `IHubContext<GameHub, IGameHubClient>`
- **Delete:** `Hubs/AppHub.cs`
- **Modify:** `Program.cs` — register `GameRoomService`, `SignalRGameBroadcaster`, map `/hubs/game`
- **Modify:** `Services/OutboxNotificationDispatcher.cs` — switch to `IHubContext<GameHub, IGameHubClient>`, handle `GameEnded` type, remove Item types

### AvaloniaApp
- `Services/IGameHubService.cs` — game-specific hub methods + events
- `Services/GameHubService.cs` — extends `HubConnectionService` with game SignalR calls
- `ViewModels/LobbyViewModel.cs` — passphrase input, create/join, ready toggle, player list
- `ViewModels/GameViewModel.cs` — holds latest `GameStateDto`, flap command, countdown display
- `ViewModels/LeaderboardViewModel.cs` — list of scores from server
- `Controls/GameCanvas.cs` — Avalonia `Control` subclass, overrides `Render`, draws world
- `Views/LobbyView.axaml` + `LobbyView.axaml.cs`
- `Views/GameView.axaml` + `GameView.axaml.cs` — hosts `GameCanvas`, flap button / spacebar
- `Views/LeaderboardView.axaml` + `LeaderboardView.axaml.cs`
- **Modify:** `ViewModels/MainWindowViewModel.cs` — replace Item logic with view navigation (Lobby → Game → Leaderboard)
- **Modify:** `Views/MainWindow.axaml` — replace item list with `ContentControl` bound to current view
- **Delete:** `ViewModels/ItemViewModel.cs`

---

## Dependency Sequencing

```
Step 1: Contracts (IGameHub, IGameHubClient, game DTOs)
Step 2: Domain (GameScore entity; remove Item)
Step 3: Application interfaces + commands (depends on Domain + Contracts)
Step 4: Infrastructure GameRoomService + GamePhysicsEngine + DB migration
Step 5: Server GameHub + Program.cs update
Step 6: Client GameHubService
Step 7: Client ViewModels (Lobby, Game, Leaderboard)
Step 8: Client Views + GameCanvas
Step 9: Wire navigation in MainWindowViewModel + MainWindow
Step 10: Smoke test build
```

---

## Preserved Implementation Rules

- **SignalR only for business logic** — no REST for game actions
- **martinothamar/Mediator** for mediator-path commands/queries only — `[GenerateMediator]`, `ICommand<T>`, `ICommandHandler<,>`
- **Direct-path** for latency-critical, stateless hub methods (AD-9) — call `IGameRoomService` directly from `GameHub`
- **Idempotency** on `JoinRoomCommand` (idempotency key prevents double-join on reconnect)
- **Outbox** for `GameEnded` notification persistence (score saving is transactional)
- **`[Authorize]`** on `GameHub` class
- **DTOs only in `AiDemo.Contracts`**
- **`IGameBroadcaster`** — Infrastructure never references Server directly (AD-10)
- **`PeriodicTimer`** for game loops — async-native, no reentrant ticks (AD-11)
- **Avalonia UI thread** — all ViewModel property updates must occur on `Dispatcher.UIThread`
- **`FlapAsync`** is fire-and-forget from client (`SendAsync`, not `InvokeAsync`) to minimize latency

## Critical Implementation Rules

### 1. SignalR Only for Business Logic
```csharp
// ✅ CORRECT - Use SignalR hub methods
await _hubConnection.InvokeAsync<GameRoomDto>("CreateOrJoinRoomAsync", dto);

// ❌ WRONG - No REST for business logic
app.MapPost("/api/rooms", ...);
```

### 2. martinothamar/Mediator Syntax (v3.0.1)
```csharp
// ✅ CORRECT - Mediator v3 (no [GenerateMediator] needed)
public sealed record JoinRoomCommand(
    string Passphrase,
    string DisplayName,
    Guid UserId,
    Guid? IdempotencyKey = null
) : ICommand<GameRoomDto>;

// ❌ WRONG - MediatR syntax
public record JoinRoomCommand : IRequest<GameRoomDto>  // NO!
```

### 3. DTOs Location
- **All DTOs go in `AiDemo.Contracts/DTOs/`**
- Never in Domain or Application layer
- Use `sealed record` for all DTOs

### 4. Idempotency Keys
All mediator-path mutating commands should support idempotency:
```csharp
public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
{
    Guid? IdempotencyKey { get; }
}
```

### 5. Outbox Pattern
Events stored in same transaction, processed by background service:
```csharp
// In handler - add to outbox
_context.OutboxMessages.Add(new OutboxMessage
{
    Type = "GameEnded",
    Payload = JsonSerializer.Serialize(resultDto)
});
await _context.SaveChangesAsync(ct);  // Atomic with score persistence
```

## NuGet Packages Reference

### AiDemo.Domain
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
```

### AiDemo.Contracts
```xml
<!-- No dependencies - pure DTOs and interfaces -->
```

### AiDemo.Application
```xml
<PackageReference Include="Mediator.Abstractions" Version="3.0.1" />
<PackageReference Include="Mediator.SourceGenerator" Version="3.0.1" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="FluentValidation" Version="11.11.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
```

### AiDemo.Infrastructure
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

### AiDemo.Server
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="10.0.0" />
<PackageReference Include="MessagePack.AspNetCoreMvcFormatter" Version="3.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
```

### AvaloniaApp (additions)
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0" />
<PackageReference Include="MessagePack" Version="3.0.0" />
<PackageReference Include="IdentityModel.OidcClient" Version="6.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

## Authentik Configuration (Local Development)

### Application: `ai-demo`
- **OAuth2 Provider**: `ai-demo` (public, PKCE enabled)
- **Client ID**: `ai-demo-desktop`
- **Redirect URIs**: `aidemo://callback`, `http://localhost:*/callback`

### Authentik Admin
- Default URL: `http://localhost:9000`
- Bootstrap password set via `AUTHENTIK_BOOTSTRAP_PASSWORD` env var

## Connection Strings

### Development
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=aidemo;Username=postgres;Password=postgres"
  },
  "Oidc": {
    "Authority": "http://localhost:9000/application/o/ai-demo",
    "ClientId": "ai-demo-desktop"
  }
}
```

## Important Implementation Notes

1. **JWT Token in SignalR**: Access token passed via query string for WebSocket connections
2. **MessagePack Protocol**: Enable for efficient binary serialization
3. **Automatic Reconnection**: Configure retry policy with exponential backoff
4. **Health Checks**: Only endpoints outside SignalR - `/health`, `/health/ready`, `/health/live`
5. **Token Refresh**: `/api/auth/refresh` endpoint for refreshing without reconnection

## Entity Definitions

### GameScore Entity (NEW)
```csharp
public sealed class GameScore
{
    public Guid Id { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime AchievedAt { get; set; }
}
```

### OutboxMessage Entity (KEEP)
```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

### IdempotencyRecord Entity (KEEP)
```csharp
public sealed class IdempotencyRecord
{
    public Guid Key { get; init; }
    public string OperationType { get; init; } = default!;
    public string? ResponseJson { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

## Testing Strategy

- **Unit Tests**: Focus on handlers (Application layer)
- **Integration Tests**: SignalR hub methods with in-memory database
- **No mocking EF Core**: Use in-memory database or Testcontainers

## Plan Review: 2026-02-26

All findings from the CRUD→Flappy Bird plan review have been integrated. See `agent-action-todo.md` Plan Review table for the resolution summary of all 9 findings.
