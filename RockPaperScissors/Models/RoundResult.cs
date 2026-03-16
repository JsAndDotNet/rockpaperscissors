namespace RockPaperScissors.Models;

public class RoundResult
{
    public bool IsDraw { get; set; }
    public string? WinnerId { get; set; }
    public string? WinnerName { get; set; }
    public string Player1Name { get; set; } = "";
    public Selection? Player1Selection { get; set; }
    public string Player2Name { get; set; } = "";
    public Selection? Player2Selection { get; set; }
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
}
