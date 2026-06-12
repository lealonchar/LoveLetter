using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Services;

public class GameEngine
{
    // Round setup
    public void StartRound(GameRoom room)
    {
        var deck = Deck.Shuffle(Deck.CreateDeck());

        // Reset player state
        foreach (var p in room.Players)
        {
            p.Hand = null;
            p.IsEliminated = false;
            p.IsProtected = false;
            p.Discards.Clear();
        }

        // Set aside one face-down card
        room.SetAsideCard = deck[0];
        deck.RemoveAt(0);

        room.DrawPile = deck;

        // Deal one card to each player
        foreach (var p in room.Players)
            p.Hand = DrawCard(room);

        room.Phase = GamePhase.Playing;
        room.CurrentPlayerIndex = 0;
        room.DrawnCard = null;
        room.PendingAction = null;
        room.ChancellorOptions.Clear();
        room.ChancellorPlayerId = null;
        room.Log.Clear();
        room.Log.Add("New round started.");
    }

    public Card DrawCard(GameRoom room)
    {
        if (room.DrawPile.Count == 0)
            throw new InvalidOperationException("Draw pile is empty.");
        var card = room.DrawPile[0];
        room.DrawPile.RemoveAt(0);
        return card;
    }
    
    public void BeginTurn(GameRoom room)
    {
        if (room.DrawPile.Count == 0)
        {
            CheckRoundEnd(room);
            return;
        }
        var player = room.CurrentPlayer!;
        player.IsProtected = false;
        room.DrawnCard = DrawCard(room);
    }

    public string PlayCard(GameRoom room, string playerId, CardType cardType,
        string? targetId, CardType? guessedCard)
    {
        if (room.PendingAction == "Chancellor")
            throw new InvalidOperationException("Waiting for Chancellor resolution.");

        var player = room.Players.First(p => p.Id == playerId);

        var hand = player.Hand!;
        var drawn = room.DrawnCard!;
        bool playingHand = cardType == hand.Type;
        var toPlay = playingHand ? hand : drawn;
        var toKeep = playingHand ? drawn : hand;

        if (MustPlayCountess(hand.Type, drawn.Type) && cardType != CardType.Countess)
            throw new InvalidOperationException("You must play the Countess when holding the Prince or King.");

        // Discard chosen card
        player.Discards.Add(toPlay);
        player.Hand = toKeep;
        room.DrawnCard = null;

        string log = ApplyCardEffect(room, player, toPlay, targetId, guessedCard);
        room.Log.Add(log);

        CheckRoundEnd(room);
        if (room.Phase == GamePhase.Playing && room.PendingAction == null)
            AdvanceTurn(room);

        return log;
    }

    private bool MustPlayCountess(CardType a, CardType b) =>
        (a == CardType.Countess && (b == CardType.King || b == CardType.Prince)) ||
        (b == CardType.Countess && (a == CardType.King || a == CardType.Prince));

    private void AdvanceTurn(GameRoom room)
    {
        int count = room.Players.Count;
        int next = (room.CurrentPlayerIndex + 1) % count;
        int tries = 0;
        while (room.Players[next].IsEliminated && tries < count)
        {
            next = (next + 1) % count;
            tries++;
        }
        room.CurrentPlayerIndex = next;
    }
    
    private string ApplyCardEffect(GameRoom room, Player actor, Card card,
        string? targetId, CardType? guessedCard)
    {
        var target = targetId != null ? room.Players.FirstOrDefault(p => p.Id == targetId) : null;

        return card.Type switch
        {
            CardType.Spy        => EffectSpy(actor),
            CardType.Guard      => EffectGuard(room, actor, target, guessedCard),
            CardType.Priest     => EffectPriest(actor, target),
            CardType.Baron      => EffectBaron(room, actor, target),
            CardType.Handmaid   => EffectHandmaid(actor),
            CardType.Prince     => EffectPrince(room, actor, target),
            CardType.Chancellor => EffectChancellor(room, actor),
            CardType.King       => EffectKing(actor, target),
            CardType.Countess   => $"{actor.Name} played the Countess.",
            CardType.Princess   => EffectPrincess(room, actor),
            _                   => "Unknown card played."
        };
    }

    private string EffectSpy(Player actor)
    {
        return $"{actor.Name} played the Spy.";
    }

    private string EffectGuard(GameRoom room, Player actor, Player? target, CardType? guess)
    {
        if (target == null || target.IsProtected)
            return $"{actor.Name} played Guard but had no valid target.";
        if (guess == null || guess == CardType.Guard)
            return $"{actor.Name} played Guard but made an invalid guess.";

        if (target.Hand!.Type == guess)
        {
            Eliminate(room, target);
            return $"{actor.Name} correctly guessed {target.Name} holds {guess}! {target.Name} is eliminated.";
        }
        return $"{actor.Name} guessed {target.Name} holds {guess} — wrong!";
    }

    private string EffectPriest(Player actor, Player? target)
    {
        if (target == null || target.IsProtected)
            return $"{actor.Name} played Priest but had no valid target.";
        return $"{actor.Name} played Priest and looked at {target.Name}'s hand.";
    }

    private string EffectBaron(GameRoom room, Player actor, Player? target)
    {
        if (target == null || target.IsProtected)
            return $"{actor.Name} played Baron but had no valid target.";

        int av = actor.Hand!.Value, tv = target.Hand!.Value;
        if (av > tv) { Eliminate(room, target); return $"{actor.Name} won the Baron comparison. {target.Name} is eliminated."; }
        if (tv > av) { Eliminate(room, actor);  return $"{target.Name} won the Baron comparison. {actor.Name} is eliminated."; }
        return $"{actor.Name} and {target.Name} tied in Baron comparison — nobody eliminated.";
    }

    private string EffectHandmaid(Player actor)
    {
        actor.IsProtected = true;
        return $"{actor.Name} played Handmaid and is protected until their next turn.";
    }

    private string EffectPrince(GameRoom room, Player actor, Player? target)
    {
        var victim = target ?? actor;
        if (victim.IsProtected && victim != actor)
            return $"{actor.Name} played Prince but target is protected.";

        var discarded = victim.Hand!;
        victim.Discards.Add(discarded);

        if (discarded.Type == CardType.Princess)
        {
            victim.Hand = null;
            Eliminate(room, victim);
            return $"{actor.Name} forced {victim.Name} to discard the Princess — {victim.Name} is eliminated!";
        }

        victim.Hand = room.DrawPile.Count > 0 ? DrawCard(room) : room.SetAsideCard!;
        return $"{actor.Name} played Prince. {victim.Name} discarded {discarded.Name} and drew a new card.";
    }

    private string EffectChancellor(GameRoom room, Player actor)
    {
        if (room.DrawPile.Count == 0)
            return $"{actor.Name} played Chancellor but the draw pile is empty.";

        var options = new List<Card> { actor.Hand! };
        actor.Hand = null; // hand is in limbo until they choose
        if (room.DrawPile.Count >= 1) options.Add(DrawCard(room));
        if (room.DrawPile.Count >= 1) options.Add(DrawCard(room));

        room.ChancellorOptions = options;
        room.ChancellorPlayerId = actor.Id;
        room.PendingAction = "Chancellor";

        return $"{actor.Name} played Chancellor and must choose a card to keep.";
    }

    private string EffectKing(Player actor, Player? target)
    {
        if (target == null || target.IsProtected)
            return $"{actor.Name} played King but had no valid target.";
        (actor.Hand, target.Hand) = (target.Hand, actor.Hand);
        return $"{actor.Name} played King and traded hands with {target.Name}.";
    }

    private string EffectPrincess(GameRoom room, Player actor)
    {
        Eliminate(room, actor);
        return $"{actor.Name} discarded the Princess and is eliminated!";
    }

    private void Eliminate(GameRoom room, Player player)
    {
        player.IsEliminated = true;
        if (player.Hand != null)
        {
            player.Discards.Add(player.Hand);
            player.Hand = null;
        }
    }

    // Round / game end

    public void CheckRoundEnd(GameRoom room)
    {
        var alive = room.Players.Where(p => !p.IsEliminated).ToList();

        bool deckEmpty = room.DrawPile.Count == 0;
        bool oneLeft   = alive.Count == 1;

        if (!oneLeft && !deckEmpty) return;

        room.Phase = GamePhase.RoundOver;

        Player roundWinner;
        if (oneLeft)
        {
            roundWinner = alive[0];
            room.Log.Add($"{roundWinner.Name} is the last player standing!");
        }
        else
        {
            roundWinner = alive
                .OrderByDescending(p => p.Hand!.Value)
                .ThenByDescending(p => p.Discards.Sum(c => c.Value))
                .First();
            room.Log.Add($"Deck exhausted! {roundWinner.Name} wins with the highest card ({roundWinner.Hand!.Name}).");
        }

        // Spy bonus — must be before winner token so game over check is accurate
        var aliveSpy = room.Players
            .Where(p => !p.IsEliminated && p.Discards.Any(d => d.Type == CardType.Spy))
            .ToList();

        if (aliveSpy.Count == 1)
        {
            aliveSpy[0].Tokens++;
            room.Log.Add($"{aliveSpy[0].Name} is the only surviving Spy player and gains a bonus token!");
        }

        roundWinner.Tokens++;
        room.Log.Add($"{roundWinner.Name} gains an affection token ({roundWinner.Tokens}/{room.RoundsToWin}).");

        // Check game over for both the round winner and a potential Spy bonus winner
        var gameWinner = room.Players.FirstOrDefault(p => p.Tokens >= room.RoundsToWin);
        if (gameWinner != null)
        {
            room.Phase = GamePhase.GameOver;
            room.Log.Add($"{gameWinner.Name} wins the game!");
        }
    }
}