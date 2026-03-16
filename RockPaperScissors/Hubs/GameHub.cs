using Microsoft.AspNetCore.SignalR;
using RockPaperScissors.Models;
using RockPaperScissors.Services;

namespace RockPaperScissors.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;
    private readonly GameManager _gameManager;

    public GameHub(GameService gameService, GameManager gameManager)
    {
        _gameService = gameService;
        _gameManager = gameManager;
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────

    public async Task SubscribeToLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
        var games = _gameService.GetAvailableGames();
        await Clients.Caller.SendAsync("LobbyUpdated", games);
    }

    public async Task UnsubscribeFromLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
    }

    // ── Game lifecycle ─────────────────────────────────────────────────────────

    public async Task CreateGame(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("Error", "Player name is required.");
            return;
        }

        var game = _gameService.CreateGame(playerName.Trim(), Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, game.Id);
        await Clients.Caller.SendAsync("GameCreated", game.Id, playerName.Trim());

        await BroadcastLobbyUpdate();
    }

    public async Task JoinGame(string gameId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            await Clients.Caller.SendAsync("Error", "Player name is required.");
            return;
        }

        var game = _gameService.JoinGame(gameId, playerName.Trim(), Context.ConnectionId);
        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found or already full.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, game.Id);

        // Notify player 1 they are player 1; player 2 they are player 2
        await Clients.Client(game.Player1.ConnectionId)
            .SendAsync("GameReady", game.Player1.Name, game.Player2!.Name, true);
        await Clients.Client(game.Player2.ConnectionId)
            .SendAsync("GameReady", game.Player1.Name, game.Player2.Name, false);

        await BroadcastLobbyUpdate();
        _gameManager.StartRound(game.Id);
    }

    // ── Gameplay ───────────────────────────────────────────────────────────────

    public async Task MakeSelection(string gameId, string selectionStr)
    {
        if (!Enum.TryParse<Selection>(selectionStr, out var selection))
        {
            await Clients.Caller.SendAsync("Error", "Invalid selection.");
            return;
        }

        bool bothSelected = _gameService.MakeSelection(gameId, Context.ConnectionId, selection);
        await Clients.Caller.SendAsync("SelectionConfirmed", selectionStr);

        if (bothSelected)
            _gameService.CancelRoundTimer(gameId);
    }

    public async Task ExitGame(string gameId)
    {
        var (removedGameId, playerName) = _gameService.RemovePlayerByConnection(Context.ConnectionId);
        if (removedGameId is null) return;

        // Remove the exiting player from the group first so they don't receive their own notification
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, removedGameId);

        // Notify whoever is left in the group
        await Clients.Group(removedGameId).SendAsync("OpponentLeft", playerName ?? "A player");

        await BroadcastLobbyUpdate();
    }

    // ── Disconnect ─────────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (gameId, playerName) = _gameService.RemovePlayerByConnection(Context.ConnectionId);
        if (gameId is not null)
        {
            await Clients.Group(gameId).SendAsync("OpponentLeft", playerName ?? "A player");
        }

        await BroadcastLobbyUpdate();
        await base.OnDisconnectedAsync(exception);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task BroadcastLobbyUpdate()
    {
        var games = _gameService.GetAvailableGames();
        await Clients.Group("lobby").SendAsync("LobbyUpdated", games);
    }
}
