namespace LoveLetter.Domain.Models;

public class Player
{
    public string Id { get; init; } // SignalR connection ID (or "AI-{name}")
    public string Name { get; set; }
    public bool IsAi { get; init; }
    public Card? Hand { get; set; } // Current card in hand
    public bool IsEliminated { get; set; }
    public bool IsProtected { get; set; } // Handmaid protection
    public List<Card> Discards { get; set; } = [];
    public int Tokens { get; set; } // Win/favor tokens

    public Player(string id, string name, bool isAi = false)
    {
        Id = id;
        Name = name;
        IsAi = isAi;
    }
}




