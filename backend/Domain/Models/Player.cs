namespace LoveLetter.Domain.Models;

public class Player
{
    public string Id { get; init; }
    public string? ConnectionId { get; set; }
    public string Name { get; set; }
    public bool IsAi { get; set; }
    public Card? Hand { get; set; } // Current card in hand
    public bool IsEliminated { get; set; }
    public bool IsProtected { get; set; } // Handmaid protection
    public List<Card> Discards { get; set; } = [];
    public int Tokens { get; set; } // Win/favor tokens

    public Player(string id, string name, bool isAi = false, string? connectionId = null)
    {
        Id = id;
        Name = name;
        IsAi = isAi;
        ConnectionId = connectionId;
    }
}




