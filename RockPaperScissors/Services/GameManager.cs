using Microsoft.AspNetCore.SignalR;
using RockPaperScissors.Hubs;
using RockPaperScissors.Models;

namespace RockPaperScissors.Services;

public class GameManager
{
    private readonly GameService _gameService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<GameManager> _logger;

    public GameManager(GameService gameService, IHubContext<GameHub> hubContext, ILogger<GameManager> logger)
    {
        _gameService = gameService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public void StartRound(string gameId)
    {
        _ = RunRound(gameId);
    }

    private async Task RunRound(string gameId)
    {
        try
        {
            var game = _gameService.GetGame(gameId);
            if (game is null || game.Phase == GamePhase.Abandoned) return;

            game.RoundCts?.Cancel();
            game.RoundCts?.Dispose();

            var cts = new CancellationTokenSource();
            game.RoundCts = cts;
            game.Phase = GamePhase.SelectionPhase;
            game.ResetSelections();

            await _hubContext.Clients.Group(gameId)
                .SendAsync("RoundStarted", game.RoundNumber);

            // 10-second selection phase with per-second ticks
            for (int i = 10; i >= 1; i--)
            {
                if (cts.IsCancellationRequested) break;

                await _hubContext.Clients.Group(gameId)
                    .SendAsync("TimerTick", i);

                try
                {
                    await Task.Delay(1000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            game = _gameService.GetGame(gameId);
            if (game is null || game.Phase == GamePhase.Abandoned) return;

            game.Phase = GamePhase.CountdownPhase;
            await _hubContext.Clients.Group(gameId)
                .SendAsync("SelectionsFrozen");

            // 3-2-1 reveal countdown
            for (int i = 3; i >= 1; i--)
            {
                await _hubContext.Clients.Group(gameId)
                    .SendAsync("Countdown", i);
                await Task.Delay(1000);
            }

            game = _gameService.GetGame(gameId);
            if (game is null || game.Phase == GamePhase.Abandoned) return;

            game.Phase = GamePhase.ResultPhase;
            var result = _gameService.CalculateRoundResult(gameId);
            if (result is null) return;

            await _hubContext.Clients.Group(gameId)
                .SendAsync("RoundResult", result);

            // Display result for 4 seconds then start next round
            await Task.Delay(4000);

            game = _gameService.GetGame(gameId);
            if (game is null || game.Phase == GamePhase.Abandoned) return;

            game.RoundNumber++;
            StartRound(gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running round for game {GameId}", gameId);
        }
    }
}
