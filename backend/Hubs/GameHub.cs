using System;
using LoveLetter.Domain.Dto;
using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoveLetter.Hubs;

using Microsoft.AspNetCore.SignalR;
using LoveLetter.Services;

public class GameHub : Hub
{
    private readonly RoomManager _rooms;
    private readonly GameEngine _engine;
    private readonly IHubContext<GameHub> _hubContext;
    private const int MaxAiTurnsPerRun = 100;
    private const int TurnReadPauseMs = 1800;
    private const int DisconnectReplacementGraceMs = 12000;
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AiPlayer>> AiPlayers = new();

    public GameHub(RoomManager rooms, GameEngine engine, IHubContext<GameHub> hubContext)
    {
        _rooms = rooms;
        _engine = engine;
        _hubContext = hubContext;
    }

    private static async Task<IDisposable> LockRoom(GameRoom room)
    {
        await room.StateLock.WaitAsync();
        return new RoomLock(room);
    }

    private sealed class RoomLock(GameRoom room) : IDisposable
    {
        public void Dispose() => room.StateLock.Release();
    }

    // Connection lifecycle

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var room = _rooms.GetRoomByPlayer(Context.ConnectionId);
        if (room != null)
        {
            var roomCode = room.Code;
            Player? disconnectedPlayer;
            var shouldReplaceWithBot = false;
            using (await LockRoom(room))
            {
                var result = _rooms.DisconnectPlayer(Context.ConnectionId);
                disconnectedPlayer = result.player;
                if (disconnectedPlayer != null && result.room != null)
                {
                    shouldReplaceWithBot = result.room.Phase == GamePhase.Playing;
                    await BroadcastState(result.room);
                }
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);

            if (disconnectedPlayer != null)
            {
                await _hubContext.Clients.Group(roomCode)
                    .SendAsync("PlayerLeft", disconnectedPlayer.Id);

                if (shouldReplaceWithBot)
                    QueueDisconnectedPlayerReplacement(roomCode, disconnectedPlayer.Id);
            }
        }
        await base.OnDisconnectedAsync(ex);
    }

    private void QueueDisconnectedPlayerReplacement(string roomCode, string playerId)
    {
        _ = ReplaceDisconnectedPlayerAfterGrace(roomCode, playerId);
    }

    private async Task ReplaceDisconnectedPlayerAfterGrace(string roomCode, string playerId)
    {
        try
        {
            await Task.Delay(DisconnectReplacementGraceMs);

            var room = _rooms.GetRoomByCode(roomCode);
            if (room == null) return;

            using (await LockRoom(room))
            {
                if (room.Phase != GamePhase.Playing) return;

                var replacement = _rooms.ReplaceDisconnectedPlayerWithAi(roomCode, playerId);
                if (replacement == null) return;

                if (room.Players.Count == 0 || room.Players.All(p => p.IsAi))
                {
                    RemoveAiLogic(roomCode);
                    return;
                }

                EnsureAiLogic(room, replacement);
                room.Log.Add($"{replacement.Name} disconnected and was replaced by a bot.");
                await BroadcastState(room);
                await _hubContext.Clients.Group(roomCode).SendAsync("PlayerLeft", replacement.Id);
                await RunAiTurn(room);
            }
        }
        catch
        {
            // Background disconnect cleanup should never crash the hub pipeline.
        }
    }

    // Lobby 

    public async Task CreateRoom(string playerName, string playerId)
    {
        var room = _rooms.CreateRoom(playerId, Context.ConnectionId, playerName);
        using (await LockRoom(room))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
            await Clients.Caller.SendAsync("RoomCreated", room.Code);
            await BroadcastState(room);
        }
    }

    public async Task<bool> JoinRoom(string code, string playerName, string playerId)
    {
        var roomToJoin = _rooms.GetRoomByCode(code);
        if (roomToJoin == null)
        {
            await Clients.Caller.SendAsync("Error", "Room not found.");
            return false;
        }

        using (await LockRoom(roomToJoin))
        {
            var (room, error) = _rooms.JoinRoom(code, playerId, Context.ConnectionId, playerName);
            if (error != null)
            {
                await Clients.Caller.SendAsync("Error", error);
                return false;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, room!.Code);
            await BroadcastState(room);
            return true;
        }
    }

    public async Task ReconnectToRoom(string roomCode, string playerId)
    {
        var roomToJoin = _rooms.GetRoomByCode(roomCode);
        if (roomToJoin == null)
        {
            await Clients.Caller.SendAsync("ReconnectFailed", "Room not found.");
            return;
        }

        using (await LockRoom(roomToJoin))
        {
            var (room, _, error) = _rooms.ReconnectPlayer(roomCode, playerId, Context.ConnectionId);
            if (error != null) { await Clients.Caller.SendAsync("ReconnectFailed", error); return; }

            await Groups.AddToGroupAsync(Context.ConnectionId, room!.Code);
            await BroadcastState(room);
        }
    }

    public async Task LeaveRoom()
    {
        var room = _rooms.GetRoomByPlayer(Context.ConnectionId);
        if (room == null)
        {
            await Clients.Caller.SendAsync("LeftRoom");
            return;
        }

        using (await LockRoom(room))
        {
            var player = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (player == null)
            {
                await Clients.Caller.SendAsync("LeftRoom");
                return;
            }

            var roomCode = room.Code;
            if (room.Phase == GamePhase.Playing)
            {
                var (replacementRoom, replacement) = _rooms.ReplacePlayerWithAi(Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
                await Clients.Caller.SendAsync("LeftRoom");

                if (replacementRoom == null ||
                    replacement == null ||
                    replacementRoom.Players.Count == 0 ||
                    replacementRoom.Players.All(p => p.IsAi))
                {
                    RemoveAiLogic(roomCode);
                    return;
                }

                EnsureAiLogic(replacementRoom, replacement);
                replacementRoom.Log.Add($"{replacement.Name} left the game and was replaced by a bot.");
                await BroadcastState(replacementRoom);
                await _hubContext.Clients.Group(roomCode).SendAsync("PlayerLeft", replacement.Id);
                await RunAiTurn(replacementRoom);
                return;
            }

            var removedIndex = room.Players.FindIndex(p => p.Id == player.Id);
            var currentIndex = room.CurrentPlayerIndex;
            var wasCurrentPlayer = room.CurrentPlayer?.Id == player.Id;
            var (updatedRoom, removedPlayer) = _rooms.RemovePlayer(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            await Clients.Caller.SendAsync("LeftRoom");

            if (updatedRoom == null ||
                removedPlayer == null ||
                updatedRoom.Players.Count == 0 ||
                updatedRoom.Players.All(p => p.IsAi))
            {
                RemoveAiLogic(roomCode);
                return;
            }

            RepositionCurrentPlayer(updatedRoom, removedIndex, currentIndex, wasCurrentPlayer);
            updatedRoom.Log.Add($"{removedPlayer.Name} left the game.");
            await BroadcastState(updatedRoom);
            await _hubContext.Clients.Group(roomCode).SendAsync("PlayerLeft", removedPlayer.Id);
            await RunAiTurn(updatedRoom);
        }
    }

    public async Task AddAiPlayer(string roomCode)
    {
        var roomToUpdate = _rooms.GetRoomByCode(roomCode);
        if (roomToUpdate == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

        using (await LockRoom(roomToUpdate))
        {
            var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (requester == null) { await Clients.Caller.SendAsync("Error", "Player not found."); return; }

            var (room, error) = _rooms.AddAiPlayer(roomCode, requester.Id);
            if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

            var aiPlayer = room!.Players.Last(p => p.IsAi);
            EnsureAiLogic(room, aiPlayer);
            await BroadcastState(room);
        }
    }

    public async Task RenameAiPlayer(string roomCode, string aiPlayerId, string name)
    {
        var roomToUpdate = _rooms.GetRoomByCode(roomCode);
        if (roomToUpdate == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

        using (await LockRoom(roomToUpdate))
        {
            var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (requester == null) { await Clients.Caller.SendAsync("Error", "Player not found."); return; }

            var (room, error) = _rooms.RenameAiPlayer(roomCode, requester.Id, aiPlayerId, name);
            if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

            await BroadcastState(room!);
        }
    }

    public async Task RemoveAiPlayer(string roomCode, string aiPlayerId)
    {
        var roomToUpdate = _rooms.GetRoomByCode(roomCode);
        if (roomToUpdate == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

        using (await LockRoom(roomToUpdate))
        {
            var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (requester == null) { await Clients.Caller.SendAsync("Error", "Player not found."); return; }

            var (room, removedPlayer, error) = _rooms.RemoveAiPlayer(roomCode, requester.Id, aiPlayerId);
            if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

            RemoveAiLogic(room!.Code, removedPlayer!.Id);
            await BroadcastState(room);
        }
    }

    public async Task StartGame(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }
        using (await LockRoom(room))
        {
            var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (requester == null || room.HostId != requester.Id)
            { await Clients.Caller.SendAsync("Error", "Only the host can start the game."); return; }
            if (room.Players.Count < 3)
            { await Clients.Caller.SendAsync("Error", "Need at least 3 players."); return; }

            StartRound(room);
            _engine.BeginTurn(room); // ← deal drawn card to first player
            await BroadcastState(room);
            await RunAiTurn(room);
        }
    }

    // Gameplay 

    // Called by a human player to play a card.
    public async Task PlayCard(string roomCode, string cardType, string? targetId, string? guessedCard)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

        using (await LockRoom(room))
        {
            var player = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (player == null || room.CurrentPlayer?.Id != player.Id)
            { await Clients.Caller.SendAsync("Error", "It's not your turn."); return; }

            if (!Enum.TryParse<CardType>(cardType, out var ct))
            { await Clients.Caller.SendAsync("Error", "Invalid card."); return; }

            CardType? guess = guessedCard != null && Enum.TryParse<CardType>(guessedCard, out var g) ? g : null;

            try
            {
                _engine.PlayCard(room, player.Id, ct, targetId, guess);
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", ex.Message);
                return;
            }

            // If Priest was played, send the target's card privately to the actor
            if (ct == CardType.Priest && targetId != null && targetId != player.Id)
            {
                var target = room.Players.FirstOrDefault(p => p.Id == targetId);
                if (target is { Hand: not null, IsEliminated: false, IsProtected: false })
                    await Clients.Caller.SendCoreAsync(
                        "PriestReveal",
                        new object?[] { targetId, target.Hand.Type.ToString() });
            }

            await HandlePostPlay(room);
        }
    }

    public async Task StartNextRound(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) return;

        using (await LockRoom(room))
        {
            var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (requester == null || room.HostId != requester.Id) return;
            if (room.Phase != GamePhase.RoundOver) return;

            StartRound(room);
            _engine.BeginTurn(room); // ← deal drawn card to first player
            await BroadcastState(room);
            await RunAiTurn(room);
        }
    }

    // Helpers 

    private void StartRound(GameRoom room)
    {
        _engine.StartRound(room);

        var roomAiLogic = new ConcurrentDictionary<string, AiPlayer>();
        foreach (var aiPlayer in room.Players.Where(p => p.IsAi))
            roomAiLogic[aiPlayer.Id] = new AiPlayer();

        AiPlayers[room.Code] = roomAiLogic;
    }

    private static AiPlayer EnsureAiLogic(GameRoom room, Player aiPlayer)
    {
        var roomAiLogic = AiPlayers.GetOrAdd(
            room.Code,
            _ => new ConcurrentDictionary<string, AiPlayer>());

        return roomAiLogic.GetOrAdd(aiPlayer.Id, _ => new AiPlayer());
    }

    private static void RemoveAiLogic(string roomCode) =>
        AiPlayers.TryRemove(roomCode, out _);

    private static void RemoveAiLogic(string roomCode, string aiPlayerId)
    {
        if (AiPlayers.TryGetValue(roomCode, out var roomAiLogic))
            roomAiLogic.TryRemove(aiPlayerId, out _);
    }

    private async Task HandlePostPlay(GameRoom room)
    {
        if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver)
        {
            await BroadcastState(room);
            return;
        }

        // Wait for Chancellor resolution before advancing
        if (room.PendingAction == GameEngine.ChancellorPendingAction)
        {
            await BroadcastState(room);
            return;
        }

        await BroadcastState(room);
        await Task.Delay(TurnReadPauseMs);

        _engine.BeginTurn(room);
        await BroadcastState(room);
        await RunAiTurn(room);
    }

    // If the current player is AI, run its turn automatically.
    private async Task RunAiTurn(GameRoom room)
    {
        int safety = 0;
        while (room.Phase == GamePhase.Playing &&
               room.CurrentPlayer?.IsAi == true &&
               safety++ < MaxAiTurnsPerRun)
        {
            await Task.Delay(800);

            var ai = room.CurrentPlayer;
            if (ai == null || ai.IsEliminated)
                break;

            if (room.PendingAction == GameEngine.ChancellorPendingAction)
            {
                if (room.ChancellorPlayerId != ai.Id)
                    break;

                await BroadcastState(room);
                await Task.Delay(TurnReadPauseMs);

                if (!ResolveAiChancellor(room, ai))
                {
                    _engine.AdvanceTurn(room);
                    _engine.BeginTurn(room);
                    await BroadcastState(room);
                    continue;
                }

                if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver) break;
                _engine.AdvanceTurn(room);
                _engine.BeginTurn(room);
                await BroadcastState(room);
                continue;
            }

            if (room.DrawnCard == null)
            {
                _engine.BeginTurn(room);
                if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver || room.DrawnCard == null)
                    break;
            }

            if (!AiPlayers.TryGetValue(room.Code, out var roomAis) ||
                !roomAis.TryGetValue(ai.Id, out var aiLogic))
                break;

            var (card, target, guess) = aiLogic.DecideAction(room, ai);

            try
            {
                _engine.PlayCard(room, ai.Id, card, target, guess);
                UpdateAiKnowledgeAfterPlay(room, aiLogic, card, target);
            }
            catch (Exception ex)
            {
                if (!TryPlayAiFallback(room, ai, aiLogic, out var fallbackError))
                {
                    room.Log.Add($"{ai.Name} could not play a valid card ({fallbackError ?? ex.Message}) and skipped their turn.");
                    room.DrawnCard = null;
                    _engine.AdvanceTurn(room);
                    if (room.Phase == GamePhase.Playing)
                        _engine.BeginTurn(room);
                    await BroadcastState(room);
                    continue;
                }
            }

            if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver) break;

            // AI resolves Chancellor automatically
            if (room.PendingAction == GameEngine.ChancellorPendingAction)
            {
                await BroadcastState(room);
                await Task.Delay(TurnReadPauseMs);

                if (!ResolveAiChancellor(room, ai))
                {
                    _engine.AdvanceTurn(room);
                    _engine.BeginTurn(room);
                    await BroadcastState(room);
                    continue;
                }

                if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver) break;
                _engine.AdvanceTurn(room);
                _engine.BeginTurn(room);
                await BroadcastState(room);
                continue;
            }

            await BroadcastState(room);
            await Task.Delay(TurnReadPauseMs);

            _engine.BeginTurn(room);
            await BroadcastState(room);
        }

        await BroadcastState(room);
    }

    private bool TryPlayAiFallback(GameRoom room, Player ai, AiPlayer aiLogic, out string? error)
    {
        error = null;
        var fallbackCards = GetAiFallbackCards(room, ai).ToList();
        if (fallbackCards.Count == 0)
            return false;

        foreach (var fallbackCard in fallbackCards)
        {
            try
            {
                _engine.PlayCard(room, ai.Id, fallbackCard, null, null);
                UpdateAiKnowledgeAfterPlay(room, aiLogic, fallbackCard, null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        return false;
    }

    private static IEnumerable<CardType> GetAiFallbackCards(GameRoom room, Player ai)
    {
        if (ai.Hand == null || room.DrawnCard == null)
            yield break;

        if (MustPlayCountess(ai.Hand.Type, room.DrawnCard.Type))
        {
            yield return CardType.Countess;
            yield break;
        }

        yield return room.DrawnCard.Type;
        if (ai.Hand.Type != room.DrawnCard.Type)
            yield return ai.Hand.Type;
    }

    private static bool MustPlayCountess(CardType a, CardType b) =>
        (a == CardType.Countess && (b == CardType.King || b == CardType.Prince)) ||
        (b == CardType.Countess && (a == CardType.King || a == CardType.Prince));

    private static void UpdateAiKnowledgeAfterPlay(
        GameRoom room,
        AiPlayer aiLogic,
        CardType playedCard,
        string? targetId)
    {
        aiLogic.CardSeen(playedCard);

        if (targetId == null)
            return;

        var target = room.Players.FirstOrDefault(p => p.Id == targetId);
        if (target == null)
            return;

        if (playedCard == CardType.Priest && target.Hand != null && !target.IsEliminated)
        {
            aiLogic.UpdateKnowledge(target.Id, target.Hand.Type);
            return;
        }

        if (playedCard is CardType.Prince or CardType.King or CardType.Baron or CardType.Guard)
            aiLogic.UpdateKnowledge(target.Id, null);
    }

    private static bool ResolveAiChancellor(GameRoom room, Player ai)
    {
        var best = room.ChancellorOptions
            .Select((card, index) => new { card, index })
            .OrderByDescending(option => option.card.Value)
            .FirstOrDefault();
        if (best == null)
        {
            ClearPendingChancellor(room);
            room.Log.Add($"{ai.Name} could not resolve Chancellor because no cards were available.");
            return false;
        }

        return ResolveChancellorChoice(room, ai, best.index, null);
    }


    // Broadcasts personalized state to each client in the room.
    private async Task BroadcastState(GameRoom room)
    {
        foreach (var player in room.Players.Where(p => !p.IsAi))
        {
            if (player.ConnectionId == null) continue;
            var dto = BuildStateDto(room, player.Id);
            await _hubContext.Clients.Client(player.ConnectionId).SendAsync("GameStateUpdated", dto);
        }
    }

    public async Task ResolveChancellor(string roomCode, int cardIndexToKeep, List<int>? returnCardIndexes)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) return;

        using (await LockRoom(room))
        {
            if (room.PendingAction != GameEngine.ChancellorPendingAction) return;
            var player = _rooms.GetPlayerByConnection(Context.ConnectionId);
            if (player == null || room.ChancellorPlayerId != player.Id) return;

            if (!ResolveChancellorChoice(room, player, cardIndexToKeep, returnCardIndexes))
            {
                await Clients.Caller.SendAsync("Error", "Invalid card choice.");
                return;
            }

            _engine.AdvanceTurn(room);
            await HandlePostPlay(room);
        }
    }

    private static bool ResolveChancellorChoice(
        GameRoom room,
        Player player,
        int keepIndex,
        IReadOnlyCollection<int>? requestedReturnIndexes)
    {
        if (keepIndex < 0 || keepIndex >= room.ChancellorOptions.Count)
            return false;

        if (!TryBuildChancellorReturnOrder(room, keepIndex, requestedReturnIndexes, out var returnIndexes))
            return false;

        var kept = room.ChancellorOptions[keepIndex];
        player.Hand = kept;
        foreach (var index in returnIndexes)
            room.DrawPile.Add(room.ChancellorOptions[index]);

        ClearPendingChancellor(room);
        room.Log.Add($"{player.Name} kept a card and returned {returnIndexes.Count} to the bottom of the deck.");
        return true;
    }

    private static bool TryBuildChancellorReturnOrder(
        GameRoom room,
        int keepIndex,
        IReadOnlyCollection<int>? requestedReturnIndexes,
        out List<int> returnIndexes)
    {
        var expectedIndexes = Enumerable.Range(0, room.ChancellorOptions.Count)
            .Where(index => index != keepIndex)
            .ToList();

        if (requestedReturnIndexes is null || requestedReturnIndexes.Count == 0)
        {
            returnIndexes = expectedIndexes;
            return true;
        }

        returnIndexes = requestedReturnIndexes.ToList();
        if (returnIndexes.Count != expectedIndexes.Count)
            return false;

        var expected = expectedIndexes.ToHashSet();
        var seen = new HashSet<int>();
        foreach (var index in returnIndexes)
        {
            if (!expected.Contains(index) || !seen.Add(index))
                return false;
        }

        return true;
    }

    private static void ClearPendingChancellor(GameRoom room)
    {
        room.ChancellorOptions.Clear();
        room.ChancellorPlayerId = null;
        room.PendingAction = null;
    }

    private static void RepositionCurrentPlayer(GameRoom room, int removedIndex, int previousCurrentIndex, bool wasCurrentPlayer)
    {
        if (room.Players.Count == 0)
        {
            room.CurrentPlayerIndex = 0;
            return;
        }

        if (removedIndex < 0)
        {
            room.CurrentPlayerIndex = Math.Clamp(room.CurrentPlayerIndex, 0, room.Players.Count - 1);
            return;
        }

        if (wasCurrentPlayer)
            room.CurrentPlayerIndex = removedIndex >= room.Players.Count ? 0 : removedIndex;
        else if (removedIndex < previousCurrentIndex)
            room.CurrentPlayerIndex = previousCurrentIndex <= 0 ? 0 : previousCurrentIndex - 1;
        else if (previousCurrentIndex >= room.Players.Count)
            room.CurrentPlayerIndex = 0;
    }

    private GameStateDto BuildStateDto(GameRoom room, string viewerId)
    {
        var viewer = room.Players.FirstOrDefault(p => p.Id == viewerId);

        var players = room.Players.Select(p => new PlayerDto(
            p.Id, p.Name, p.IsAi,
            p.IsEliminated, p.IsProtected,
            p.Discards.Count, p.Tokens,
            p.Discards,
            Hand: p.Id == viewerId ? p.Hand : null // hide others' hands
        )).ToList();

        PlayerDto? yourState = viewer == null ? null : new PlayerDto(
            viewer.Id, viewer.Name, viewer.IsAi,
            viewer.IsEliminated, viewer.IsProtected,
            viewer.Discards.Count, viewer.Tokens,
            viewer.Discards,
            Hand: viewer.Hand
        );

        List<Card> chancellorOptions;
        if (viewerId == room.ChancellorPlayerId)
            chancellorOptions = room.ChancellorOptions;
        else
            chancellorOptions = new List<Card>();

        return new GameStateDto(
            room.Code, room.Phase,
            room.HostId,
            players,
            room.DrawPile.Count,
            room.CurrentPlayerIndex,
            room.CurrentPlayer?.Name,
            yourState,
            room.Log.TakeLast(10).ToList(),
            room.RoundsToWin,
            room.PendingAction,
            chancellorOptions,
            room.RoundWinnerIds,
            room.GameWinnerIds,
            DrawnCard: room.CurrentPlayer?.Id == viewerId ? room.DrawnCard : null
        );
    }
}
