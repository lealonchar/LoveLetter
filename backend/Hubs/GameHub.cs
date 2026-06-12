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
    private static readonly Dictionary<string, Dictionary<string, AiPlayer>> AiPlayers = new();

    public GameHub(RoomManager rooms, GameEngine engine)
    {
        _rooms = rooms;
        _engine = engine;
    }

    // Connection lifecycle

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var room = _rooms.GetRoomByPlayer(Context.ConnectionId);
        if (room != null)
        {
            _rooms.RemovePlayer(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Code);
            await BroadcastState(room);
            await Clients.Group(room.Code)
                .SendAsync("PlayerLeft", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(ex);
    }

    // Lobby 

    public async Task CreateRoom(string playerName)
    {
        var room = _rooms.CreateRoom(Context.ConnectionId, playerName);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await Clients.Caller.SendAsync("RoomCreated", room.Code);
        await BroadcastState(room);
    }

    public async Task JoinRoom(string code, string playerName)
    {
        var (room, error) = _rooms.JoinRoom(code, Context.ConnectionId, playerName);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, room!.Code);
        await BroadcastState(room);
    }

    public async Task AddAiPlayer(string roomCode)
    {
        var (room, error) = _rooms.AddAiPlayer(roomCode, Context.ConnectionId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }

        var aiPlayer = room!.Players.Last(p => p.IsAi);
        if (!AiPlayers.ContainsKey(roomCode))
            AiPlayers[roomCode] = new Dictionary<string, AiPlayer>();

        AiPlayers[roomCode][aiPlayer.Id] = new AiPlayer();
        await BroadcastState(room);
    }

    public async Task StartGame(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null || room.HostId != Context.ConnectionId)
        { await Clients.Caller.SendAsync("Error", "Only the host can start the game."); return; }
        if (room.Players.Count < 3)
        { await Clients.Caller.SendAsync("Error", "Need at least 3 players."); return; }

        StartRound(room);
        _engine.BeginTurn(room);
        await BroadcastState(room);
        await RunAiTurn(room);
    }

    // Gameplay 

    // Called by a human player to play a card.
    public async Task PlayCard(string roomCode, string cardType, string? targetId, string? guessedCard)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null) { await Clients.Caller.SendAsync("Error", "Room not found."); return; }

        var player = room.Players.FirstOrDefault(p => p.Id == Context.ConnectionId);
        if (player == null || room.CurrentPlayer?.Id != Context.ConnectionId)
        { await Clients.Caller.SendAsync("Error", "It's not your turn."); return; }

        if (!Enum.TryParse<CardType>(cardType, out var ct))
        { await Clients.Caller.SendAsync("Error", "Invalid card."); return; }

        CardType? guess = guessedCard != null && Enum.TryParse<CardType>(guessedCard, out var g) ? g : null;

        try
        {
            _engine.PlayCard(room, Context.ConnectionId, ct, targetId, guess);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
            return;
        }

        // If Priest was played, send the target's card privately to the actor
        if (ct == CardType.Priest && targetId != null)
        {
            var target = room.Players.FirstOrDefault(p => p.Id == targetId);
            if (target?.Hand != null)
                await Clients.Caller.SendAsync("PriestReveal", targetId, target.Hand.Type.ToString());
        }

        await HandlePostPlay(room);
    }

    public async Task StartNextRound(string roomCode)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null || room.HostId != Context.ConnectionId) return;
        if (room.Phase != GamePhase.RoundOver) return;

        StartRound(room);
        _engine.BeginTurn(room);
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

        if (room.PendingAction == "Chancellor")
        {
            await BroadcastState(room);
            return;
        }


        if (room.DrawPile.Count == 0) { _engine.CheckRoundEnd(room); await BroadcastState(room); return; }
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
               safety++ < 20)
        {
            await Task.Delay(800);

            var ai = room.CurrentPlayer;
            
            if (room.PendingAction == "Chancellor" && room.ChancellorPlayerId == ai.Id)
            {
                var best = room.ChancellorOptions.OrderByDescending(c => c.Value).First();
                var returns = room.ChancellorOptions.Where(c => c != best).ToList();
                ai.Hand = best;
                foreach (var c in returns) room.DrawPile.Add(c);
                room.ChancellorOptions.Clear();
                room.ChancellorPlayerId = null;
                room.PendingAction = null;
                room.Log.Add($"{ai.Name} kept a card and returned {returns.Count} to the bottom of the deck.");

                if (room.DrawPile.Count == 0) { _engine.CheckRoundEnd(room); break; }
                _engine.BeginTurn(room);
                continue;
            }

            if (room.PendingAction != null) break; // unknown pending action, bail

            if (!AiPlayers.TryGetValue(room.Code, out var roomAis) ||
                !roomAis.TryGetValue(ai.Id, out var aiLogic))
                break;

            if (room.DrawnCard == null) break;

            var (card, target, guess) = aiLogic.DecideAction(room, ai);

            try
            {
                _engine.PlayCard(room, ai.Id, card, target, guess);
            }
            catch
            {
                if (room.DrawnCard != null)
                    _engine.PlayCard(room, ai.Id, room.DrawnCard.Type, null, null);
                else
                    break;
            }

            if (room.Phase is GamePhase.RoundOver or GamePhase.GameOver)
                break;

            if (room.DrawPile.Count == 0) { _engine.CheckRoundEnd(room); break; }
            _engine.BeginTurn(room);
        }

        await BroadcastState(room);
    }


    // Broadcasts personalized state to each client in the room.
    private async Task BroadcastState(GameRoom room)
    {
        foreach (var player in room.Players.Where(p => !p.IsAi))
        {
            var dto = BuildStateDto(room, player.Id);
            await Clients.Client(player.Id).SendAsync("GameStateUpdated", dto);
        }
    }
    
    public async Task ResolveChancellor(string roomCode, string cardTypeToKeep)
    {
        var room = _rooms.GetRoomByCode(roomCode);
        if (room == null || room.PendingAction != "Chancellor") return;
        if (room.ChancellorPlayerId != Context.ConnectionId) return;

        if (!Enum.TryParse<CardType>(cardTypeToKeep, out var keep)) return;

        var player = room.Players.First(p => p.Id == Context.ConnectionId);
        var kept = room.ChancellorOptions.FirstOrDefault(c => c.Type == keep);
        if (kept == null)
        {
            await Clients.Caller.SendAsync("Error", "Invalid card choice.");
            return;
        }
        var returns = room.ChancellorOptions.Where(c => c != kept).ToList();

        player.Hand = kept;
        foreach (var c in returns)
            room.DrawPile.Add(c); // return to bottom

        room.ChancellorOptions.Clear();
        room.ChancellorPlayerId = null;
        room.PendingAction = null;

        room.Log.Add($"{player.Name} kept a card and returned {returns.Count} to the bottom of the deck.");

        await HandlePostPlay(room);
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
