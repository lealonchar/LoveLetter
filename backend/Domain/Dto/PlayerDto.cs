using LoveLetter.Domain.Models;

using System.Collections.Generic;

namespace LoveLetter.Domain.Dto;

public record PlayerDto(
    string Id,
    string Name,
    bool IsAi,
    bool IsEliminated,
    bool IsProtected,
    int DiscardCount,
    int Tokens,
    List<Card> Discards,
    Card? Hand = null // Only populated for the requesting player
);
