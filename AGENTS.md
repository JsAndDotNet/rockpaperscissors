# Rock Paper Scissors – AI Agent Instructions

## Overview
This is a real-time multiplayer Rock Paper Scissors game built with:
- **Blazor Server** (.NET 8) – interactive server-side Razor components
- **SignalR** – WebSocket-based real-time communication hub
- **In-memory storage** – `GameService` holds all game state via `ConcurrentDictionary`

No database or external storage is used. All state lives in the running process.

---

## Project Structure

```
RockPaperScissors/
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor        # App shell (header + main slot)
│   ├── Pages/
│   │   ├── Home.razor              # Lobby – game list + create/join modals
│   │   └── Game.razor              # Game play page
│   └── _Imports.razor              # Global @using directives
├── Hubs/
│   └── GameHub.cs                  # SignalR hub – all client↔server messaging
├── Models/
│   ├── Game.cs                     # Aggregate: players, phase, round number
│   ├── GameInfo.cs                 # Lightweight lobby-list DTO
│   ├── GamePhase.cs                # Enum: WaitingForPlayer, SelectionPhase, …
│   ├── Player.cs                   # Player: name, connection, score, selection
│   ├── RoundResult.cs              # Round result DTO sent to clients
│   └── Selection.cs                # Enum: Rock, Paper, Scissors
├── Services/
│   ├── GameService.cs              # In-memory CRUD + game-logic helpers
│   └── GameManager.cs              # Orchestrates async round flow (timers, phases)
├── wwwroot/
│   └── app.css                     # Global CSS – blue theme, design tokens
├── Program.cs                      # DI registration + route mapping
Dockerfile                          # Multi-stage Docker build
docker-compose.yml                  # Single-service compose file
AGENTS.md                           # This file
```

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Blazor Server + SignalR | Real-time push without a separate API; SignalR over WebSockets is the natural transport for Blazor Server |
| `ConcurrentDictionary` for games | Thread-safe; no DB needed; games are short-lived |
| `GameManager` separate from `GameHub` | Hub methods stay thin; async round loop lives in a singleton service injected via `IHubContext` |
| `GameClientPhase` enum in `Game.razor` | Client-side phase tracking maps 1:1 to server events, making UI state management predictable |
| Single `/gamehub` endpoint | All game and lobby events share one hub to keep connection count low |

---

## SignalR Message Contract

### Client → Server (Hub methods)

| Method | Parameters | Description |
|---|---|---|
| `SubscribeToLobby` | — | Join "lobby" group; receive `LobbyUpdated` |
| `UnsubscribeFromLobby` | — | Leave "lobby" group |
| `CreateGame` | `playerName` | Creates game; server replies `GameCreated` |
| `JoinGame` | `gameId, playerName` | Joins existing game; server replies `GameReady` to both |
| `MakeSelection` | `gameId, selectionStr` | Submit Rock/Paper/Scissors |
| `ExitGame` | `gameId` | Graceful exit; notifies opponent |

### Server → Client (events)

| Event | Payload | Trigger |
|---|---|---|
| `LobbyUpdated` | `GameInfo[]` | Any game created, joined, or abandoned |
| `GameCreated` | `gameId, playerName` | Sent to creator after `CreateGame` |
| `GameReady` | `p1Name, p2Name, isPlayer1` | Sent to both players when second joins |
| `RoundStarted` | `roundNumber` | Start of each new round |
| `TimerTick` | `secondsRemaining` | Every second during 10-s selection window |
| `SelectionConfirmed` | `selectionStr` | Sent to the player who just selected |
| `SelectionsFrozen` | — | Timer expired; selections locked |
| `Countdown` | `count (3→1)` | Reveal countdown ticks |
| `RoundResult` | `RoundResult` | Full result + updated scores |
| `OpponentLeft` | `playerName` | The other player disconnected or exited |
| `Error` | `message` | Validation or state error |

---

## Running Locally

```bash
cd RockPaperScissors
dotnet run
# Open http://localhost:5122 in two browser windows
```

## Running with Docker

```bash
docker compose up --build
# Open http://localhost:8080
```

---

## Common Extension Points for Future AI Work

- **Persistence**: Replace `ConcurrentDictionary` in `GameService` with an EF Core `DbContext` (add `IGameRepository` interface first).
- **Best-of-N rounds**: Add a `MaxRounds` property to `Game` and end the game when a player reaches `(MaxRounds/2)+1` wins.
- **Spectator mode**: Add a "spectate" SignalR group per game; mirror `RoundResult` events there.
- **Rematch**: After `OpponentLeft` / game end, allow the winner to start a new game with the same players.
- **Player authentication**: Add ASP.NET Core Identity; tie `Player.Id` to the authenticated user.
- **Rate limiting**: Add `Microsoft.AspNetCore.RateLimiting` middleware to the hub endpoint.
