using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Domain.Dto;

public record GameStateDto(
    string RoomCode,
    GamePhase Phase,
    string HostId,
    List<PlayerDto> Players,
    int DrawPileCount,
    int CurrentPlayerId_Index,
    string? CurrentPlayerName,
    PlayerDto? YourState,
    List<string> Log,
    int RoundsToWin,
    string? PendingAction,
    List<Card> ChancellorOptions,
    Card? DrawnCard
);
