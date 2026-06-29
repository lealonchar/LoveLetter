using LoveLetter.Domain.Dto;
using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Hubs;

using Microsoft.AspNetCore.SignalR;
using LoveLetter.Services;

public class GameHub : Hub
{
    private readonly RoomManager _rooms;
    private readonly GameEngine _engine;
    private const int MaxAiTurnsPerRun = 100;
    private static readonly Dictionary<string, Dictionary<string, AiPlayer>> AiPlayers = new();

    public GameHub(RoomManager rooms, GameEngine engine)
    {
        _rooms = rooms;
        _engine = engine;
    }

    // Connection lifecycle

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var room = _rooms.DisconnectPlayer(Context.ConnectionId);
        if (room != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Code);

            if (room.Players.Count > 0)
            {
                await BroadcastState(room);
            }

            await Clients.Group(room.Code)
                .SendAsync("PlayerLeft", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(ex);
    }

    // Lobby 

    public async Task CreateRoom(string playerName, string playerId)
    {
        var room = _rooms.CreateRoom(playerId, Context.ConnectionId, playerName);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await Clients.Caller.SendAsync("RoomCreated", room.Code);
        await BroadcastState(room);
    }

    public async Task JoinRoom(string code, string playerName, string playerId)
    {
        var (room, error) = _rooms.JoinRoom(code, playerId, Context.ConnectionId, playerName);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, room!.Code);
        await BroadcastState(room);
    }

    public async Task ReconnectToRoom(string roomCode, string playerId)
    {
        var (room, _, error) = _rooms.ReconnectPlayer(roomCode, playerId, Context.ConnectionId);
        if (error != null) { await Clients.Caller.SendAsync("ReconnectFailed", error); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, room!.Code);
        await BroadcastState(room);
    }

    public async Task LeaveRoom()
    {
        var room = _rooms.GetRoomByPlayer(Context.ConnectionId);
        var player = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (room == null || player == null)
        {
            await Clients.Caller.SendAsync("LeftRoom");
            return;
        }

        var roomCode = room.Code;
        var removedIndex = room.Players.FindIndex(p => p.Id == player.Id);
        var currentIndex = room.CurrentPlayerIndex;
        var wasCurrentPlayer = room.CurrentPlayer?.Id == player.Id;
        var wasPendingChancellor = room.ChancellorPlayerId == player.Id;

        var (updatedRoom, removedPlayer) = _rooms.RemovePlayer(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
        await Clients.Caller.SendAsync("LeftRoom");

        if (updatedRoom == null ||
            removedPlayer == null ||
            updatedRoom.Players.Count == 0 ||
            updatedRoom.Players.All(p => p.IsAi))
            return;

        if (updatedRoom.Phase == GamePhase.Playing)
        {
            RepositionCurrentPlayer(updatedRoom, removedIndex, currentIndex, wasCurrentPlayer);

            if (wasPendingChancellor)
            {
                foreach (var card in updatedRoom.ChancellorOptions)
                    updatedRoom.DrawPile.Add(card);
                ClearPendingChancellor(updatedRoom);
            }

            if (wasCurrentPlayer)
            {
                updatedRoom.DrawnCard = null;
                _engine.CheckRoundEnd(updatedRoom);
                if (updatedRoom.Phase == GamePhase.Playing)
                    _engine.BeginTurn(updatedRoom);
            }
            else
            {
                _engine.CheckRoundEnd(updatedRoom);
            }
        }

        updatedRoom.Log.Add($"{removedPlayer.Name} left the game.");
        await BroadcastState(updatedRoom);
        await Clients.Group(roomCode).SendAsync("PlayerLeft", removedPlayer.Id);
        await RunAiTurn(updatedRoom);
    }

    public async Task AddAiPlayer(string roomCode)
    {
        var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (requester == null) { await Clients.Caller.SendAsync("Error", "Player not found."); return; }

        var (room, error) = _rooms.AddAiPlayer(roomCode, requester.Id);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

        var aiPlayer = room!.Players.Last(p => p.IsAi);
        if (!AiPlayers.ContainsKey(roomCode))
            AiPlayers[roomCode] = new Dictionary<string, AiPlayer>();

        AiPlayers[roomCode][aiPlayer.Id] = new AiPlayer();
        await BroadcastState(room);
    }

    public async Task RenameAiPlayer(string roomCode, string aiPlayerId, string name)
    {
        var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (requester == null) { await Clients.Caller.SendAsync("Error", "Player not found."); return; }

        var (room, error) = _rooms.RenameAiPlayer(roomCode, requester.Id, aiPlayerId, name);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

        await BroadcastState(room!);
    }

    public async Task StartGame(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (room == null || requester == null || room.HostId != requester.Id)
        { await Clients.Caller.SendAsync("Error", "Only the host can start the game."); return; }
        if (room.Players.Count < 3)
        { await Clients.Caller.SendAsync("Error", "Need at least 3 players."); return; }

        StartRound(room);
        _engine.BeginTurn(room); // ← deal drawn card to first player
        await BroadcastState(room);
        await RunAiTurn(room);
    }

    // Gameplay 

    // Called by a human player to play a card.
    public async Task PlayCard(string roomCode, string cardType, string? targetId, string? guessedCard)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

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
                await Clients.Caller.SendAsync("PriestReveal", targetId, target.Hand.Type.ToString());
        }

        await HandlePostPlay(room);
    }

    public async Task StartNextRound(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        var requester = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (room == null || requester == null || room.HostId != requester.Id) return;
        if (room.Phase != GamePhase.RoundOver) return;

        StartRound(room);
        _engine.BeginTurn(room); // ← deal drawn card to first player
        await BroadcastState(room);
        await RunAiTurn(room);
    }

    // Helpers 

    private void StartRound(GameRoom room)
    {
        _engine.StartRound(room);
        if (!AiPlayers.ContainsKey(room.Code))
            AiPlayers[room.Code] = new Dictionary<string, AiPlayer>();

        // Reset every AI player's knowledge for the new round
        foreach (var aiPlayer in room.Players.Where(p => p.IsAi))
            AiPlayers[room.Code][aiPlayer.Id] = new AiPlayer();
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
            }
            catch
            {
                if (room.DrawnCard == null)
                    break;

                try
                {
                    _engine.PlayCard(room, ai.Id, room.DrawnCard.Type, null, null);
                }
                catch
                {
                    room.Log.Add($"{ai.Name} could not play a valid card and skipped their turn.");
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
                var best = room.ChancellorOptions.OrderByDescending(c => c.Value).First();
                ResolveChancellorChoice(room, ai, best);

                if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver) break;
                _engine.AdvanceTurn(room);
                _engine.BeginTurn(room);
                await BroadcastState(room);
                continue;
            }

            _engine.BeginTurn(room);
            await BroadcastState(room);
        }

        await BroadcastState(room);
    }


    // Broadcasts personalized state to each client in the room.
    private async Task BroadcastState(GameRoom room)
    {
        foreach (var player in room.Players.Where(p => !p.IsAi))
        {
            if (player.ConnectionId == null) continue;
            var dto = BuildStateDto(room, player.Id);
            await Clients.Client(player.ConnectionId).SendAsync("GameStateUpdated", dto);
        }
    }
    
    public async Task ResolveChancellor(string roomCode, string cardTypeToKeep)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null || room.PendingAction != GameEngine.ChancellorPendingAction) return;
        var player = _rooms.GetPlayerByConnection(Context.ConnectionId);
        if (player == null || room.ChancellorPlayerId != player.Id) return;

        if (!Enum.TryParse<CardType>(cardTypeToKeep, out var keep)) return;

        var kept = room.ChancellorOptions.FirstOrDefault(c => c.Type == keep);
        if (kept == null)
        {
            await Clients.Caller.SendAsync("Error", "Invalid card choice.");
            return;
        }
        ResolveChancellorChoice(room, player, kept);

        _engine.AdvanceTurn(room);
        await HandlePostPlay(room);
    }

    private static void ResolveChancellorChoice(GameRoom room, Player player, Card kept)
    {
        var returns = room.ChancellorOptions.Where(c => c != kept).ToList();

        player.Hand = kept;
        foreach (var card in returns)
            room.DrawPile.Add(card);

        ClearPendingChancellor(room);
        room.Log.Add($"{player.Name} kept a card and returned {returns.Count} to the bottom of the deck.");
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
            room.CurrentPlayerIndex = Math.Max(0, previousCurrentIndex - 1);
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
            viewerId == room.ChancellorPlayerId ? room.ChancellorOptions : [],
            DrawnCard: room.CurrentPlayer?.Id == viewerId ? room.DrawnCard : null
        );
    }
}
