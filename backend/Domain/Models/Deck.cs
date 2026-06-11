using LoveLetter.Domain.Enums;

namespace LoveLetter.Domain.Models;

public static class Deck
{
    public static List<Card> CreateDeck() =>
    [
        // 2x Spy
        new(CardType.Spy), new(CardType.Spy),
        // 6x Guard
        new(CardType.Guard), new(CardType.Guard), new(CardType.Guard),
        new(CardType.Guard), new(CardType.Guard), new(CardType.Guard),
        // 2x Priest
        new(CardType.Priest), new(CardType.Priest),
        // 2x Baron
        new(CardType.Baron), new(CardType.Baron),
        // 2x Handmaid
        new(CardType.Handmaid), new(CardType.Handmaid),
        // 2x Prince
        new(CardType.Prince), new(CardType.Prince),
        // 2x Chancellor
        new(CardType.Chancellor), new(CardType.Chancellor),
        // 1x King
        new(CardType.King),
        // 1x Countess
        new(CardType.Countess),
        // 1x Princess
        new(CardType.Princess),
    ];

    // Fisher-Yates shuffle
    public static List<Card> Shuffle(List<Card> deck)
    {
        var rng = new Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }
}