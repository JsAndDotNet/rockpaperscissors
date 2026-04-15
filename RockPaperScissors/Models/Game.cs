namespace RockPaperScissors.Models;

public class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public Player Player1 { get; set; } = new();
    public Player? Player2 { get; set; }
    public GameMode Mode { get; set; } = GameMode.LizardSpock;
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayer;
    public int RoundNumber { get; set; } = 1;
    public CancellationTokenSource? RoundCts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool BothPlayersPresent => Player2 is not null;

    public bool BothSelected =>
        Player1.CurrentSelection.HasValue &&
        Player2?.CurrentSelection.HasValue == true;

    public void ResetSelections()
    {
        Player1.CurrentSelection = null;
        if (Player2 is not null)
            Player2.CurrentSelection = null;
    }

    public GameInfo ToGameInfo() => new()
    {
        Id = Id,
        CreatorName = Player1.Name,
        CreatedAt = CreatedAt,
        Mode = Mode
    };
}
