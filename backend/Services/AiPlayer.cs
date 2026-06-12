using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Services;

// Rule-based AI for Love Letter.
// Maintains a knowledge tracker per game and picks moves by priority rules.
public class AiPlayer
{
    private readonly Dictionary<string, CardType?> _knownHands = new();
    private readonly HashSet<CardType> _seenCards = new();

    public void UpdateKnowledge(string playerId, CardType? knownCard) =>
        _knownHands[playerId] = knownCard;

    public void CardSeen(CardType card) =>
        _seenCards.Add(card);

    public (CardType cardToPlay, string? targetId, CardType? guardGuess)
        DecideAction(GameRoom room, Player me)
    {
        if (me.Hand == null || room.DrawnCard == null)
            return (CardType.Spy, null, null); 
        
        var hand = me.Hand!;
        var drawn = room.DrawnCard!;
        var opponents = room.Players
            .Where(p => !p.IsEliminated && p.Id != me.Id && !p.IsProtected)
            .ToList();

        // Must play Countess if holding King or Prince
        if ((hand.Type == CardType.Countess && (drawn.Type == CardType.King || drawn.Type == CardType.Prince)) ||
            (drawn.Type == CardType.Countess && (hand.Type == CardType.King || hand.Type == CardType.Prince)))
            return (CardType.Countess, null, null);

        var handPlay = EvaluatePlay(room, me, hand, opponents);
        var drawnPlay = EvaluatePlay(room, me, drawn, opponents);

        return handPlay.score >= drawnPlay.score ? handPlay.move : drawnPlay.move;
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        EvaluatePlay(GameRoom room, Player me, Card card, List<Player> opponents)
    {
        switch (card.Type)
        {
            case CardType.Spy:
                // Spy has no immediate effect — low priority, just play it if nothing better
                return ((CardType.Spy, null, null), 15);

            case CardType.Guard:
            {
                foreach (var (pid, known) in _knownHands)
                {
                    if (known == null || known == CardType.Guard) continue;
                    var target = opponents.FirstOrDefault(p => p.Id == pid);
                    if (target != null)
                        return ((CardType.Guard, target.Id, known), 100);
                }
                if (opponents.Any())
                {
                    var best = GuessHighValueCard(room);
                    return ((CardType.Guard, opponents[0].Id, best), 20);
                }
                return ((CardType.Guard, null, null), 0);
            }

            case CardType.Priest:
            {
                if (opponents.Any())
                    return ((CardType.Priest, opponents[0].Id, null), 30);
                return ((CardType.Priest, null, null), 0);
            }

            case CardType.Baron:
            {
                var otherCard = me.Hand!.Type == card.Type ? room.DrawnCard! : me.Hand;
                if (opponents.Any())
                {
                    var weakest = opponents.OrderBy(p =>
                        _knownHands.TryGetValue(p.Id, out var k) ? (k.HasValue ? (int)k : 4) : 4).First();
                    int score = otherCard.Value >= 5 ? 60 : (otherCard.Value >= 3 ? 35 : 10);
                    return ((CardType.Baron, weakest.Id, null), score);
                }
                return ((CardType.Baron, null, null), 0);
            }

            case CardType.Handmaid:
                return ((CardType.Handmaid, null, null), 25);

            case CardType.Prince:
            {
                if (opponents.Any())
                {
                    var victim = opponents.OrderBy(p =>
                        _knownHands.TryGetValue(p.Id, out var k) ? (k.HasValue ? (int)k : 4) : 4).First();
                    return ((CardType.Prince, victim.Id, null), 40);
                }
                return ((CardType.Prince, me.Id, null), 15);
            }

            case CardType.Chancellor:
            {
                // Chancellor is always good — drawing extra cards improves your position
                // Score higher if current hand card is weak
                var otherCard = me.Hand!.Type == card.Type ? room.DrawnCard! : me.Hand;
                int chancellorScore = otherCard.Value <= 3 ? 55 : 35;
                return ((CardType.Chancellor, null, null), chancellorScore);
            }

            case CardType.King:
            {
                var otherCard = me.Hand!.Type == card.Type ? room.DrawnCard! : me.Hand;
                if (opponents.Any() && otherCard.Value <= 3)
                    return ((CardType.King, opponents[0].Id, null), 45);
                return ((CardType.King, null, null), 5);
            }

            case CardType.Countess:
                return ((CardType.Countess, null, null), 10);

            case CardType.Princess:
                return ((CardType.Princess, null, null), -999);

            default:
                return ((card.Type, null, null), 0);
        }
    }

    private CardType GuessHighValueCard(GameRoom room)
    {
        var allDiscarded = room.Players
            .SelectMany(p => p.Discards)
            .Select(c => c.Type)
            .Concat(_seenCards)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        var fullCounts = new Dictionary<CardType, int>
        {
            [CardType.Spy]        = 2,
            [CardType.Guard]      = 5,
            [CardType.Priest]     = 2,
            [CardType.Baron]      = 2,
            [CardType.Handmaid]   = 2,
            [CardType.Prince]     = 2,
            [CardType.Chancellor] = 2,
            [CardType.King]       = 1,
            [CardType.Countess]   = 1,
            [CardType.Princess]   = 1,
        };

        return fullCounts
            .Where(kv => kv.Key != CardType.Guard)
            .Where(kv => !allDiscarded.TryGetValue(kv.Key, out int d) || d < kv.Value)
            .OrderByDescending(kv => (int)kv.Key)
            .Select(kv => kv.Key)
            .FirstOrDefault(CardType.Priest);
    }
}