namespace RockPaperScissors.Models;

public class Player
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public Selection? CurrentSelection { get; set; }
    public int Score { get; set; }
}
