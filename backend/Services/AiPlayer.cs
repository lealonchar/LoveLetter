using System;
using System.Collections.Generic;
using System.Linq;
using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Services;

public class AiPlayer
{
    private static readonly Dictionary<CardType, int> FullCounts = new()
    {
        [CardType.Spy] = 2,
        [CardType.Guard] = 6,
        [CardType.Priest] = 2,
        [CardType.Baron] = 2,
        [CardType.Handmaid] = 2,
        [CardType.Prince] = 2,
        [CardType.Chancellor] = 2,
        [CardType.King] = 1,
        [CardType.Countess] = 1,
        [CardType.Princess] = 1,
    };

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

        var hand = me.Hand;
        var drawn = room.DrawnCard;

        if (MustPlayCountess(hand.Type, drawn.Type))
            return (CardType.Countess, null, null);

        var handPlay = EvaluatePlay(room, me, hand, drawn);
        var drawnPlay = EvaluatePlay(room, me, drawn, hand);

        return handPlay.score >= drawnPlay.score ? handPlay.move : drawnPlay.move;
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        EvaluatePlay(GameRoom room, Player me, Card cardToPlay, Card cardToKeep)
    {
        var targets = ValidTargets(room, me).ToList();
        var deckPressureThreshold = room.Players.Count < 2 ? 2 : room.Players.Count;
        var deckPressure = room.DrawPile.Count <= deckPressureThreshold;

        return cardToPlay.Type switch
        {
            CardType.Princess => ((CardType.Princess, null, null), -10_000),
            CardType.Countess => ((CardType.Countess, null, null), ScoreCountess(cardToKeep)),
            CardType.Spy => ((CardType.Spy, null, null), ScoreSpy(me, cardToKeep, deckPressure)),
            CardType.Guard => ScoreGuard(room, targets),
            CardType.Priest => ScorePriest(targets),
            CardType.Baron => ScoreBaron(me, cardToKeep, targets),
            CardType.Handmaid => ((CardType.Handmaid, null, null), ScoreHandmaid(me, cardToKeep, deckPressure)),
            CardType.Prince => ScorePrince(room, me, cardToKeep, targets),
            CardType.Chancellor => ((CardType.Chancellor, null, null), ScoreChancellor(room, cardToKeep)),
            CardType.King => ScoreKing(cardToKeep, targets),
            _ => ((cardToPlay.Type, null, null), 0)
        };
    }

    private static bool MustPlayCountess(CardType a, CardType b) =>
        (a == CardType.Countess && (b == CardType.King || b == CardType.Prince)) ||
        (b == CardType.Countess && (a == CardType.King || a == CardType.Prince));

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        ScoreGuard(GameRoom room, List<Player> targets)
    {
        var knownTarget = targets
            .Select(t => (player: t, known: KnownHand(t)))
            .Where(x => x.known is not null and not CardType.Guard)
            .OrderByDescending(x => CardValue(x.known!.Value) + ThreatScore(x.player))
            .FirstOrDefault();

        if (knownTarget.player != null)
            return ((CardType.Guard, knownTarget.player.Id, knownTarget.known), 115);

        var target = targets.OrderByDescending(ThreatScore).FirstOrDefault();
        if (target == null)
            return ((CardType.Guard, null, null), 4);

        var guess = BestGuardGuess(room);
        var score = 28 + Math.Min(18, ThreatScore(target));
        return ((CardType.Guard, target.Id, guess), score);
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        ScorePriest(List<Player> targets)
    {
        var target = targets
            .Where(t => KnownHand(t) == null)
            .OrderByDescending(ThreatScore)
            .FirstOrDefault()
            ?? targets.OrderByDescending(ThreatScore).FirstOrDefault();

        if (target == null)
            return ((CardType.Priest, null, null), 5);

        return ((CardType.Priest, target.Id, null), 34 + Math.Min(16, ThreatScore(target)));
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        ScoreBaron(Player me, Card cardToKeep, List<Player> targets)
    {
        var knownSafeTarget = targets
            .Select(t => (player: t, known: KnownHand(t)))
            .Where(x => x.known != null && CardValue(x.known.Value) < cardToKeep.Value)
            .OrderByDescending(x => ThreatScore(x.player))
            .FirstOrDefault();

        if (knownSafeTarget.player != null)
            return ((CardType.Baron, knownSafeTarget.player.Id, null), 85 + ThreatScore(knownSafeTarget.player));

        if (cardToKeep.Value <= 2)
            return ((CardType.Baron, null, null), 3);

        var target = targets
            .OrderBy(t => ExpectedHandValue(t))
            .ThenByDescending(ThreatScore)
            .FirstOrDefault();

        if (target == null)
            return ((CardType.Baron, null, null), 5);

        var confidence = cardToKeep.Value >= 6 ? 55 : cardToKeep.Value >= 4 ? 30 : 12;
        return ((CardType.Baron, target.Id, null), confidence + Math.Min(12, ThreatScore(target)));
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        ScorePrince(GameRoom room, Player me, Card cardToKeep, List<Player> targets)
    {
        var knownPrincess = targets.FirstOrDefault(t => KnownHand(t) == CardType.Princess);
        if (knownPrincess != null)
            return ((CardType.Prince, knownPrincess.Id, null), 120);

        if (cardToKeep.Value <= 2 && room.DrawPile.Count > 0)
            return ((CardType.Prince, me.Id, null), 45);

        var target = targets
            .Where(t => ExpectedHandValue(t) >= 5)
            .OrderByDescending(t => ExpectedHandValue(t) + ThreatScore(t))
            .FirstOrDefault();

        if (target != null)
            return ((CardType.Prince, target.Id, null), 46 + Math.Min(14, ThreatScore(target)));

        if (cardToKeep.Value <= 3 && room.DrawPile.Count > 0)
            return ((CardType.Prince, me.Id, null), 28);

        return ((CardType.Prince, null, null), 12);
    }

    private ((CardType cardToPlay, string? targetId, CardType? guardGuess) move, int score)
        ScoreKing(Card cardToKeep, List<Player> targets)
    {
        if (cardToKeep.Value >= 5)
            return ((CardType.King, null, null), 4);

        var target = targets
            .OrderByDescending(t => ExpectedHandValue(t) + ThreatScore(t))
            .FirstOrDefault();

        if (target == null)
            return ((CardType.King, null, null), 4);

        var expectedGain = ExpectedHandValue(target) - cardToKeep.Value;
        var score = expectedGain > 0 ? 34 + (int)(expectedGain * 5) : 8;
        return ((CardType.King, target.Id, null), score);
    }

    private int ScoreCountess(Card cardToKeep) =>
        cardToKeep.Value >= 7 ? 6 : 18;

    private int ScoreSpy(Player me, Card cardToKeep, bool deckPressure)
    {
        var alreadyPlayedSpy = me.Discards.Any(c => c.Type == CardType.Spy);
        if (alreadyPlayedSpy)
            return 8;

        return deckPressure ? 32 : cardToKeep.Value >= 6 ? 24 : 18;
    }

    private int ScoreHandmaid(Player me, Card cardToKeep, bool deckPressure)
    {
        var holdingHighValue = cardToKeep.Value >= 7;
        var hasSpyBonusChance = me.Discards.Any(c => c.Type == CardType.Spy);

        if (holdingHighValue && deckPressure)
            return 64;
        if (holdingHighValue)
            return 48;
        if (hasSpyBonusChance && deckPressure)
            return 42;
        return 24;
    }

    private static int ScoreChancellor(GameRoom room, Card cardToKeep)
    {
        if (room.DrawPile.Count == 0)
            return 4;

        if (cardToKeep.Value <= 2)
            return 68;
        if (cardToKeep.Value <= 4)
            return 52;
        return room.DrawPile.Count >= 2 ? 36 : 24;
    }

    private IEnumerable<Player> ValidTargets(GameRoom room, Player me) =>
        room.Players.Where(p =>
            p.Id != me.Id &&
            !p.IsEliminated &&
            !p.IsProtected &&
            p.Hand != null);

    private CardType? KnownHand(Player player) =>
        _knownHands.TryGetValue(player.Id, out var known) ? known : null;

    private double ExpectedHandValue(Player player)
    {
        var known = KnownHand(player);
        if (known != null)
            return CardValue(known.Value);

        return 4.4 + Math.Min(1.5, player.Discards.Count * 0.2);
    }

    private static int ThreatScore(Player player)
    {
        var discardThreat = player.Discards.Count > 6 ? 6 : player.Discards.Count;
        return player.Tokens * 5 + discardThreat;
    }

    private static int CardValue(CardType type) => (int)type;

    private CardType BestGuardGuess(GameRoom room)
    {
        var seen = room.Players
            .SelectMany(p => p.Discards)
            .Select(c => c.Type)
            .Concat(room.Players.Where(p => p.IsEliminated && p.Hand != null).Select(p => p.Hand!.Type))
            .Concat(_seenCards)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        return FullCounts
            .Where(kv => kv.Key != CardType.Guard)
            .Select(kv =>
            {
                seen.TryGetValue(kv.Key, out var used);
                var remaining = kv.Value - used;
                return (type: kv.Key, remaining: remaining < 0 ? 0 : remaining);
            })
            .Where(x => x.remaining > 0)
            .OrderByDescending(x => x.remaining * (CardValue(x.type) + 1))
            .ThenByDescending(x => CardValue(x.type))
            .Select(x => x.type)
            .FirstOrDefault(CardType.Priest);
    }
}
