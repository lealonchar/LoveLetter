using LoveLetter.Domain.Enums;

using System.Collections.Generic;
using System.Threading;

namespace LoveLetter.Domain.Models;

public class GameRoom
{
    public string Code { get; init; }
    public string HostId { get; set; }
    public List<Player> Players { get; set; } = new List<Player>();
    public GamePhase Phase { get; set; } = GamePhase.Lobby;

    // Round state
    public List<Card> DrawPile { get; set; } = new List<Card>();
    public Card? SetAsideCard { get; set; } // Face-down removed card
    public int CurrentPlayerIndex { get; set; }
    public Card? DrawnCard { get; set; } // Card drawn this turn (before play)
    public string? PendingAction { get; set; } // Waiting for secondary input
    public int RoundsToWin => Players.Count switch { 2 => 6, 3 => 5, 4 => 4, _ => 3 };
    public List<string> RoundWinnerIds { get; set; } = new List<string>();
    public List<string> GameWinnerIds { get; set; } = new List<string>();
    public List<string> Log { get; set; } = new List<string>();
    public List<Card> ChancellorOptions { get; set; } = new List<Card>();
    public string? ChancellorPlayerId { get; set; }
    public SemaphoreSlim StateLock { get; } = new(1, 1);

    public Player? CurrentPlayer =>
        Players.Count > 0 && CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count
            ? Players[CurrentPlayerIndex]
            : null;

    public GameRoom(string code, string hostId)
    {
        Code = code;
        HostId = hostId;
    }
}
