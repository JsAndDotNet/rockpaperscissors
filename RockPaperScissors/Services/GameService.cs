using System.Collections.Concurrent;
using RockPaperScissors.Models;

namespace RockPaperScissors.Services;

public class GameService
{
    private readonly ConcurrentDictionary<string, Game> _games = new();

    public Game CreateGame(string playerName, string connectionId)
    {
        var player = new Player { Name = playerName, ConnectionId = connectionId };
        var game = new Game { Player1 = player };
        _games[game.Id] = game;
        return game;
    }

    public Game? JoinGame(string gameId, string playerName, string connectionId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;
        lock (game)
        {
            if (game.Phase != GamePhase.WaitingForPlayer || game.Player2 is not null) return null;
            game.Player2 = new Player { Name = playerName, ConnectionId = connectionId };
            game.Phase = GamePhase.SelectionPhase;
        }
        return game;
    }

    public bool MakeSelection(string gameId, string connectionId, Selection selection)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        lock (game)
        {
            if (game.Phase != GamePhase.SelectionPhase) return false;
            if (game.Player1.ConnectionId == connectionId)
                game.Player1.CurrentSelection = selection;
            else if (game.Player2?.ConnectionId == connectionId)
                game.Player2.CurrentSelection = selection;
            else
                return false;
            return game.BothSelected;
        }
    }

    public RoundResult? CalculateRoundResult(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game) || game.Player2 is null) return null;

        var p1 = game.Player1;
        var p2 = game.Player2;

        string? winnerId = null;
        string? winnerName = null;
        bool isDraw = false;

        if (!p1.CurrentSelection.HasValue && !p2.CurrentSelection.HasValue)
        {
            isDraw = true;
        }
        else if (!p1.CurrentSelection.HasValue)
        {
            winnerId = p2.Id; winnerName = p2.Name;
        }
        else if (!p2.CurrentSelection.HasValue)
        {
            winnerId = p1.Id; winnerName = p1.Name;
        }
        else if (p1.CurrentSelection == p2.CurrentSelection)
        {
            isDraw = true;
        }
        else
        {
            bool p1Wins =
                (p1.CurrentSelection == Selection.Rock     && p2.CurrentSelection == Selection.Scissors) ||
                (p1.CurrentSelection == Selection.Rock     && p2.CurrentSelection == Selection.Lizard)   ||
                (p1.CurrentSelection == Selection.Paper    && p2.CurrentSelection == Selection.Rock)     ||
                (p1.CurrentSelection == Selection.Paper    && p2.CurrentSelection == Selection.Spock)    ||
                (p1.CurrentSelection == Selection.Scissors && p2.CurrentSelection == Selection.Paper)    ||
                (p1.CurrentSelection == Selection.Scissors && p2.CurrentSelection == Selection.Lizard)   ||
                (p1.CurrentSelection == Selection.Lizard   && p2.CurrentSelection == Selection.Spock)    ||
                (p1.CurrentSelection == Selection.Lizard   && p2.CurrentSelection == Selection.Paper)    ||
                (p1.CurrentSelection == Selection.Spock    && p2.CurrentSelection == Selection.Scissors) ||
                (p1.CurrentSelection == Selection.Spock    && p2.CurrentSelection == Selection.Rock);

            if (p1Wins) { winnerId = p1.Id; winnerName = p1.Name; }
            else        { winnerId = p2.Id; winnerName = p2.Name; }
        }

        lock (game)
        {
            if (!isDraw)
            {
                if (winnerId == p1.Id) p1.Score++;
                else p2.Score++;
            }
        }

        string? resultDescription = null;
        if (!isDraw && p1.CurrentSelection.HasValue && p2.CurrentSelection.HasValue)
        {
            var winner = winnerId == p1.Id ? p1.CurrentSelection.Value : p2.CurrentSelection.Value;
            var loser  = winnerId == p1.Id ? p2.CurrentSelection.Value : p1.CurrentSelection.Value;
            resultDescription = GetWinDescription(winner, loser);
        }

        return new RoundResult
        {
            IsDraw = isDraw,
            WinnerId = winnerId,
            WinnerName = winnerName,
            Player1Name = p1.Name,
            Player1Selection = p1.CurrentSelection,
            Player2Name = p2.Name,
            Player2Selection = p2.CurrentSelection,
            Player1Score = p1.Score,
            Player2Score = p2.Score,
            ResultDescription = resultDescription
        };
    }

    private static string GetWinDescription(Selection winner, Selection loser) => (winner, loser) switch
    {
        (Selection.Rock,     Selection.Scissors) => "Rock crushes Scissors",
        (Selection.Rock,     Selection.Lizard)   => "Rock crushes Lizard",
        (Selection.Paper,    Selection.Rock)      => "Paper covers Rock",
        (Selection.Paper,    Selection.Spock)     => "Paper disproves Spock",
        (Selection.Scissors, Selection.Paper)     => "Scissors cuts Paper",
        (Selection.Scissors, Selection.Lizard)    => "Scissors decapitates Lizard",
        (Selection.Lizard,   Selection.Spock)     => "Lizard poisons Spock",
        (Selection.Lizard,   Selection.Paper)     => "Lizard eats Paper",
        (Selection.Spock,    Selection.Scissors)  => "Spock smashes Scissors",
        (Selection.Spock,    Selection.Rock)      => "Spock vaporizes Rock",
        _                                         => ""
    };

    public (string? gameId, string? playerName) RemovePlayerByConnection(string connectionId)
    {
        var game = _games.Values.FirstOrDefault(g =>
            g.Player1.ConnectionId == connectionId ||
            g.Player2?.ConnectionId == connectionId);

        if (game is null) return (null, null);

        string? playerName = game.Player1.ConnectionId == connectionId
            ? game.Player1.Name
            : game.Player2?.Name;

        game.RoundCts?.Cancel();
        game.Phase = GamePhase.Abandoned;
        _games.TryRemove(game.Id, out _);

        return (game.Id, playerName);
    }

    public void CancelRoundTimer(string gameId)
    {
        if (_games.TryGetValue(gameId, out var game))
            game.RoundCts?.Cancel();
    }

    public Game? GetGame(string gameId) =>
        _games.TryGetValue(gameId, out var g) ? g : null;

    public IReadOnlyList<GameInfo> GetAvailableGames() =>
        _games.Values
            .Where(g => g.Phase == GamePhase.WaitingForPlayer)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => g.ToGameInfo())
            .ToList();
}
