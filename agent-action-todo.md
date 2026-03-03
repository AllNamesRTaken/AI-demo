# Implementation Agent — Task List: Multiplayer Flappy Bird

## Phase 1: Contracts Layer ✅ COMPLETED 2026-02-26

- [x] Delete `IAppHub.cs`, `IAppHubClient.cs` and all Item DTOs (`ItemDto`, `CreateItemDto`, `UpdateItemDto`)
- [x] Create `AiDemo.Contracts/Enums/GameStatus.cs` — `Waiting`, `Countdown`, `Running`, `Ended` (Contracts only; GameScore doesn't reference it)
- [x] Create `AiDemo.Contracts/DTOs/Game/PlayerStateDto.cs`
- [x] Create `AiDemo.Contracts/DTOs/Game/PipeDto.cs`
- [x] Create `AiDemo.Contracts/DTOs/Game/GameStateDto.cs` — references `GameStatus` enum
- [x] Create `AiDemo.Contracts/DTOs/Game/GameRoomDto.cs`
- [x] Create `AiDemo.Contracts/DTOs/Game/GameResultDto.cs` (includes `PlayerResultDto`)
- [x] Create `AiDemo.Contracts/DTOs/Game/JoinRoomDto.cs`
- [x] Create `AiDemo.Contracts/Hubs/IGameHub.cs` — `CreateOrJoinRoomAsync`, `LeaveRoomAsync`, `SetReadyAsync`, `FlapAsync`, `GetLeaderboardAsync`
- [x] Create `AiDemo.Contracts/Hubs/IGameHubClient.cs` — game callbacks + system callbacks (`OnError`, `OnNotification`, `OnRateLimitExceeded`, `OnForceDisconnect`)
- [x] Keep `RateLimitInfo.cs`, `ErrorDto.cs`, `NotificationDto.cs`, `UserPresenceDto.cs` — still referenced by `IGameHubClient`
- [x] `build-validate AiDemo.Contracts` — **must be clean** (Contracts has no external dependencies)

## Phase 2: Domain Layer ✅ COMPLETED 2026-03-03

- [x] Delete `AiDemo.Domain/Entities/Item.cs` and `Events/ItemCreatedEvent.cs`
- [x] Create `AiDemo.Domain/Entities/GameScore.cs`
- [x] `build-validate AiDemo.Domain` — **must be clean** (Domain has no external dependencies; Application/Infrastructure/Server still have old Item code at this stage so building the full solution would fail)

## Phase 3: Application Layer

- [ ] Delete all Item commands (`CreateItem`, `UpdateItem`, `DeleteItem`) and Item queries (`GetItems`, `GetItemById`)
- [ ] Create `AiDemo.Application/Interfaces/IGameRoomService.cs` — room lifecycle + direct-path methods (`EnqueueFlap`, `SetPlayerReady`)
- [ ] Create `AiDemo.Application/Interfaces/IGameBroadcaster.cs` — abstraction over SignalR hub context (AD-10: circular dependency fix)
- [ ] Create `AiDemo.Application/Commands/JoinRoom/JoinRoomCommand.cs` + `JoinRoomHandler.cs` — mediator-path (idempotency key, validation)
- [ ] Create `AiDemo.Application/Commands/LeaveRoom/LeaveRoomCommand.cs` + `LeaveRoomHandler.cs` — mediator-path (cleanup, may outbox)
- [ ] Create `AiDemo.Application/Queries/GetLeaderboard/GetLeaderboardQuery.cs` + `GetLeaderboardHandler.cs` — mediator-path (DB query)
- [ ] Update `AiDemo.Application/Interfaces/IApplicationDbContext.cs` — replace `Items` DbSet with `GameScores`
- [ ] `build-validate server` — **compile-check only (errors expected)**: `ApplicationDbContext` in Infrastructure still declares `Items`; interface/implementation mismatch is intentional and resolved in Phase 4

> **Note (AD-9 — Direct-Path vs Mediator-Path):**
> `FlapAsync` and `SetReadyAsync` are **direct-path** — they call `IGameRoomService` directly from `GameHub`, bypassing Mediator entirely. No command/handler files are created for them.
>
> **Why?** The Mediator pipeline runs 3 behaviors per call:
> 1. `LoggingBehavior` — 2× ILogger calls + Stopwatch allocation per invocation
> 2. `ValidationBehavior` — DI resolution of `IValidator<T>`, empty iteration when nothing to validate
> 3. `IdempotencyBehavior` — queries the `IdempotencyRecords` DB table on every call
>
> For flap input at ~5 calls/sec × 8 players = 40 calls/sec, this means 40 unnecessary DB round-trips/sec + 80 log entries/sec of noise. The 50ms tick budget doesn't tolerate this.
>
> **Rule of thumb for future methods:** If the method is latency-critical, stateless (no DB), and naturally idempotent — make it direct-path. If it persists data, needs idempotency checks, or benefits from validation — use mediator-path. See AD-9 in agent-action-internal.md for the full classification criteria.

## Phase 4: Infrastructure Layer

- [ ] Create `AiDemo.Infrastructure/Services/GamePhysicsEngine.cs` — static physics helpers
- [ ] Create `AiDemo.Infrastructure/Services/GameRoomService.cs` — singleton implementing `IGameRoomService`; in-memory rooms; game loop via `PeriodicTimer` (AD-11); injects `IGameBroadcaster` (AD-10, **not** `IHubContext`)
  - `EnqueueFlap(Guid userId)` — thread-safe flap queue per player
  - `SetPlayerReady(Guid userId)` — toggle ready state, trigger countdown when all ready
  - `CreateOrJoinRoom(...)` / `LeaveRoom(...)` — room lifecycle
  - `RunGameLoopAsync(room, ct)` — async loop per active room using `PeriodicTimer(50ms)`
- [ ] Delete `AiDemo.Infrastructure/Persistence/Configurations/ItemConfiguration.cs`
- [ ] Create `AiDemo.Infrastructure/Persistence/Configurations/GameScoreConfiguration.cs`
- [ ] Update `ApplicationDbContext.cs` — remove `Items` DbSet, add `GameScores` DbSet
- [ ] Add EF Core migration: `dotnet ef migrations add AddGameScore -p AiDemo.Infrastructure -s AiDemo.Server`
- [ ] Update `AiDemo.Infrastructure/DependencyInjection.cs` — register `GameRoomService` as singleton, remove obsolete registrations
- [ ] `build-validate server` — **must be clean** (interface/implementation mismatch from Phase 3 is now resolved)

## Phase 5: Server Layer

- [ ] Delete `AiDemo.Server/Hubs/AppHub.cs`
- [ ] Create `AiDemo.Server/Hubs/GameHub.cs` implementing `IGameHub` with `[Authorize]`
  - **Direct-path methods** (call `IGameRoomService` directly):
    - `FlapAsync()` → `_roomService.EnqueueFlap(userId)`
    - `SetReadyAsync()` → `_roomService.SetPlayerReady(userId)`
  - **Mediator-path methods** (go through `IMediator.Send()`):
    - `CreateOrJoinRoomAsync(JoinRoomDto dto)` → `JoinRoomCommand` (idempotent)
    - `LeaveRoomAsync()` → `LeaveRoomCommand`
    - `GetLeaderboardAsync()` → `GetLeaderboardQuery`
  - Override `OnDisconnectedAsync` → auto-leave room
- [ ] Create `AiDemo.Server/Services/SignalRGameBroadcaster.cs` implementing `IGameBroadcaster` (uses `IHubContext<GameHub, IGameHubClient>`)
- [ ] Update `AiDemo.Server/Services/OutboxNotificationDispatcher.cs`:
  - Switch `IHubContext<AppHub, IAppHubClient>` → `IHubContext<GameHub, IGameHubClient>`
  - Remove `ItemCreated`/`ItemUpdated`/`ItemDeleted` cases
  - Add `GameEnded` case (deserialize `GameResultDto`, broadcast to room group)
- [ ] Update `AiDemo.Server/Program.cs`:
  - Map `/hubs/game` instead of `/hubs/app`
  - Register `GameRoomService` as singleton with `IGameRoomService`
  - Register `SignalRGameBroadcaster` as singleton with `IGameBroadcaster`
  - Remove Item-related service registrations
- [ ] `build-validate server` — **must be clean**

## Phase 6: Client Hub Service

- [ ] Create `AvaloniaApp/Services/IGameHubService.cs` — game SignalR events and methods
- [ ] Create `AvaloniaApp/Services/GameHubService.cs`:
  - Connects to `/hubs/game`
  - Wraps `CreateOrJoinRoomAsync`, `LeaveRoomAsync`, `SetReadyAsync`, `FlapAsync`, `GetLeaderboardAsync`
  - Exposes events: `RoomUpdated`, `GameTick`, `GameStarted`, `GameEnded`, `CountdownTick`, `PlayerJoined`, `PlayerLeft`
- [ ] Update `AvaloniaApp/Services/HubConnectionService.cs` — adjust hub URL or remove if GameHubService replaces it entirely
- [ ] Update DI registration in `App.axaml.cs` — register `GameHubService`
- [ ] `build-validate client` — **must be clean**

## Phase 7: Client ViewModels

- [ ] Delete `AvaloniaApp/ViewModels/ItemViewModel.cs`
- [ ] Create `AvaloniaApp/ViewModels/LobbyViewModel.cs`:
  - Properties: `Passphrase`, `DisplayName`, `IsInRoom`, `IsReady`, `Players` (ObservableCollection)
  - Commands: `JoinOrCreateRoomCommand`, `SetReadyCommand`, `LeaveRoomCommand`
  - Subscribes to `RoomUpdated`, `PlayerJoined`, `PlayerLeft`
- [ ] Create `AvaloniaApp/ViewModels/GameViewModel.cs`:
  - Properties: `CurrentGameState` (latest `GameStateDto`), `IsAlive`, `MyScore`, `CountdownText`, `GameStatus`
  - Commands: `FlapCommand` (maps to keyboard Space and on-screen button)
  - Subscribes to `GameTick`, `GameStarted`, `GameEnded`, `CountdownTick`
  - All property updates on `Dispatcher.UIThread`
- [ ] Create `AvaloniaApp/ViewModels/LeaderboardViewModel.cs`:
  - ObservableCollection of `PlayerResultDto`
  - Loads on navigation via `GetLeaderboardAsync`
- [ ] Update `AvaloniaApp/ViewModels/MainWindowViewModel.cs`:
  - Replace Item logic with `CurrentView` property (object)
  - Navigation methods: `ShowLobby()`, `ShowGame()`, `ShowLeaderboard()`
  - On auth success → navigate to Lobby
- [ ] `build-validate client` — **must be clean**

## Phase 8: Client Views + Game Canvas

- [ ] Create `AvaloniaApp/Controls/GameCanvas.cs`:
  - Extends Avalonia `Control`
  - Property `GameState` of type `GameStateDto?`
  - Override `Render(DrawingContext ctx)`: draw sky background, pipes, birds (colored circles per player), floor/ceiling
  - Each player's bird color derived from userId (deterministic hue)
  - Draw score text per alive bird
- [ ] Create `AvaloniaApp/Views/LobbyView.axaml`:
  - TextBox for passphrase, TextBox for display name
  - "Join / Create Room" button, player list with ready indicators
  - "Ready" toggle button, "Leave" button
  - DataContext = `LobbyViewModel`
- [ ] Create `AvaloniaApp/Views/GameView.axaml`:
  - `GameCanvas` control bound to `GameViewModel.CurrentGameState`
  - Countdown overlay (visible during countdown)
  - "FLAP" button (also responds to Space key)
  - Scoreboard sidebar showing all players' current scores
  - DataContext = `GameViewModel`
- [ ] Create `AvaloniaApp/Views/LeaderboardView.axaml`:
  - DataGrid or ListBox showing rank, name, score
  - "Play Again" button → navigate back to Lobby
  - DataContext = `LeaderboardViewModel`
- [ ] Update `AvaloniaApp/Views/MainWindow.axaml`:
  - Replace item list with `ContentControl` bound to `MainWindowViewModel.CurrentView`
  - Add DataTemplates mapping ViewModel types to View types
- [ ] `build-validate client` — **must be clean**

## Phase 9: Wire-Up & Cleanup

- [ ] Update `AI-demo.sln` if any projects were added/removed (verify solution references)
- [ ] Verify `ARCHITECTURE.md` hub URL references updated from `/hubs/app` to `/hubs/game`
- [ ] Build all projects: `dotnet build AI-demo.sln` — resolve all compile errors
- [ ] Verify `OutboxNotificationDispatcher` handles `GameEnded` message type (switches updated in Phase 5)
- [ ] Verify `OutboxProcessorService` still resolves dependencies correctly after refactoring
- [ ] Test room join + game flow manually (two instances of Avalonia app)

---

## Implementation Order (Critical Path)

```
Phase 1 (Contracts) → Phase 2 (Domain) → Phase 3 (Application) →
Phase 4 (Infrastructure) → Phase 5 (Server) → Phase 6 (Client Hub) →
Phase 7 (ViewModels) → Phase 8 (Views) → Phase 9 (Wire-up)
```

Phases 1–2 can be worked in parallel. Phases 3–5 must follow in order.

## Phase 5 (post-MVP): Production Hardening

- [ ] Rate limiting policy update for game-specific methods (FlapAsync should be rate-limited differently than JoinRoom)
- [ ] Testing infrastructure (see old 5.2 tasks, adapted for game commands)
- [ ] Graceful shutdown: notify rooms on server stop

---

## Plan Review: 2026-02-26

All 9 review findings have been integrated into the phases above:

| # | Finding | Resolution |
|---|---------|------------|
| 1 | `GameRoomService` circular dependency | AD-10: `IGameBroadcaster` in Application, `SignalRGameBroadcaster` in Server (Phase 3 + 5) |
| 2 | Flap/SetReady through Mediator is overkill | AD-9: Direct-path classification; no command/handler files (Phase 3 note + Phase 5) |
| 3 | `OutboxNotificationDispatcher` not updated | Explicit task added to Phase 5 |
| 4 | `System.Timers.Timer` | AD-11: `PeriodicTimer` in Phase 4 |
| 5 | Missing `GameStatus` enum | Added to Phase 1 (Contracts/Enums) |
| 6 | `RateLimitInfo` orphaned | Phase 1 explicitly keeps it; `IGameHubClient` carries system callbacks |
| 7 | `OutboxProcessorService` not verified | Phase 9 adds explicit verification task |
| 8 | No new NuGet packages needed | Confirmed — no changes to `.csproj` files |
| 9 | `IGameHubClient` missing system callbacks | Phase 1 specifies `OnError`, `OnNotification`, `OnRateLimitExceeded`, `OnForceDisconnect` |
