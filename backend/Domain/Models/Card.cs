using System;
using LoveLetter.Domain.Enums;

namespace LoveLetter.Domain.Models;

public class Card
{
    public CardType Type { get; init; }
    public string Name { get; init; }
    public int Value { get; init; }
    public string Description { get; init; }

    public Card(CardType type)
    {
        Type = type;
        Value = (int)type;
        (Name, Description) = type switch
        {
            CardType.Spy => ("Spy",
                "At the end of the game if you are the only player that played a spy gain 1 favor token."),
            CardType.Guard => ("Guard", "Guess a player's hand (not Guard). If correct, they're eliminated."),
            CardType.Priest => ("Priest", "Look at another player's hand."),
            CardType.Baron => ("Baron", "Compare hands with another player. Lower card is eliminated."),
            CardType.Handmaid => ("Handmaid", "Protected from effects until your next turn."),
            CardType.Prince => ("Prince", "Choose a player (including yourself) to discard and redraw."),
            CardType.Chancellor => ("Chancellor",
                "Draw 2 cards. Keep one, return the other 2 at the bottom of the deck"),
            CardType.King => ("King", "Trade hands with another player."),
            CardType.Countess => ("Countess", "Must discard if caught with King or Prince."),
            CardType.Princess => ("Princess", "Eliminated if you discard this card."),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
