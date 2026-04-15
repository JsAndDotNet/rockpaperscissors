namespace RockPaperScissors.Models;

public class GameInfo
{
    public string Id { get; set; } = "";
    public string CreatorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public GameMode Mode { get; set; } = GameMode.LizardSpock;
}
