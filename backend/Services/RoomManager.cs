using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Services;

using System.Collections.Concurrent;

public class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    // Map connection ID → room code for fast disconnect lookup
    private readonly ConcurrentDictionary<string, string> _playerRooms = new();

    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
    private static readonly string[] AiNames =
    [
        "Ada", "Bea", "Cleo", "Dante", "Elio", "Flora", "Galen", "Hana",
        "Iris", "Jules", "Kira", "Leo", "Mira", "Nico", "Opal", "Pia",
        "Quinn", "Rhea", "Silas", "Talia", "Uma", "Vera", "Wren", "Zara"
    ];

    public string GenerateCode()
    {
        string code;
        do { code = new string(Enumerable.Range(0, 6).Select(_ => Chars[Random.Shared.Next(Chars.Length)]).ToArray()); }
        while (_rooms.ContainsKey(code));
        return code;
    }

    public GameRoom CreateRoom(string hostPlayerId, string hostConnectionId, string hostName)
    {
        while (true)
        {
            var code = GenerateCode();
            var room = new GameRoom(code, hostPlayerId);
            var host = new Player(hostPlayerId, hostName, connectionId: hostConnectionId);
            room.Players.Add(host);

            if (!_rooms.TryAdd(code, room))
                continue;

            _playerRooms[hostConnectionId] = code;
            return room;
        }
    }

    public (GameRoom? room, string? error) JoinRoom(string code, string playerId, string connectionId, string playerName)
    {
        if (!_rooms.TryGetValue(code.ToUpper(), out var room))
            return (null, "Room not found.");
        if (room.Phase != GamePhase.Lobby)
            return (null, "Game already in progress.");
        if (room.Players.Count >= 6)
            return (null, "Room is full (max 6 players).");
        if (room.Players.Any(p => string.Equals(p.Name, playerName, StringComparison.OrdinalIgnoreCase)))
            return (null, "Name already taken in this room.");

        room.Players.Add(new Player(playerId, playerName, connectionId: connectionId));
        _playerRooms[connectionId] = room.Code;
        return (room, null);
    }

    public (GameRoom? room, Player? player, string? error) ReconnectPlayer(
        string code,
        string playerId,
        string connectionId)
    {
        if (!_rooms.TryGetValue(code.ToUpper(), out var room))
            return (null, null, "Room not found.");

        var player = room.Players.FirstOrDefault(p => p.Id == playerId && !p.IsAi);
        if (player == null)
            return (null, null, "Seat not found.");

        if (player.ConnectionId != null)
            _playerRooms.TryRemove(player.ConnectionId, out _);

        player.ConnectionId = connectionId;
        _playerRooms[connectionId] = room.Code;
        return (room, player, null);
    }

    public (GameRoom? room, string? error) AddAiPlayer(string code, string requesterId)
    {
        if (!_rooms.TryGetValue(code.ToUpper(), out var room))
            return (null, "Room not found.");
        if (room.HostId != requesterId)
            return (null, "Only the host can add AI players.");
        if (room.Phase != GamePhase.Lobby)
            return (null, "AI players can only be added in the lobby.");
        if (room.Players.Count >= 6)
            return (null, "Room is full.");

        var aiName = GetRandomAvailableAiName(room);
        var ai = new Player($"AI-{Guid.NewGuid():N}", aiName, isAi: true);
        room.Players.Add(ai);
        return (room, null);
    }

    public (GameRoom? room, string? error) RenameAiPlayer(
        string code,
        string requesterId,
        string aiPlayerId,
        string name)
    {
        if (!_rooms.TryGetValue(code.ToUpper(), out var room))
            return (null, "Room not found.");
        if (room.HostId != requesterId)
            return (null, "Only the host can rename AI players.");
        if (room.Phase != GamePhase.Lobby)
            return (null, "AI players can only be renamed in the lobby.");

        var ai = room.Players.FirstOrDefault(p => p.Id == aiPlayerId && p.IsAi);
        if (ai == null)
            return (null, "AI player not found.");

        var trimmed = name.Trim();
        if (trimmed.Length is < 1 or > 24)
            return (null, "Name must be 1-24 characters.");
        if (room.Players.Any(p => p.Id != aiPlayerId &&
                                  string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return (null, "Name already taken in this room.");

        ai.Name = trimmed;
        return (room, null);
    }

    public GameRoom? GetRoomByCode(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : _rooms.TryGetValue(code.ToUpper(), out var r) ? r : null;

    public GameRoom? GetRoomByPlayer(string connectionId) =>
        _playerRooms.TryGetValue(connectionId, out var code) ? GetRoomByCode(code) : null;

    public Player? GetPlayerByConnection(string connectionId)
    {
        var room = GetRoomByPlayer(connectionId);
        return room?.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
    }

    public (GameRoom? room, Player? player) DisconnectPlayer(string connectionId)
    {
        if (!_playerRooms.TryRemove(connectionId, out var code)) return (null, null);
        if (!_rooms.TryGetValue(code, out var room)) return (null, null);

        var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player != null)
            player.ConnectionId = null;

        return (room, player);
    }

    public (GameRoom? room, Player? player) RemovePlayer(string connectionId)
    {
        if (!_playerRooms.TryRemove(connectionId, out var code)) return (null, null);
        if (!_rooms.TryGetValue(code, out var room)) return (null, null);

        var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null) return (room, null);

        room.Players.RemoveAll(p => p.Id == player.Id);
        if (room.Players.Count == 0 || room.Players.All(p => p.IsAi))
            _rooms.TryRemove(code, out _);
        else if (room.HostId == player.Id)
            room.HostId = room.Players.FirstOrDefault(p => !p.IsAi)?.Id
                          ?? room.Players.First().Id; // Reassign host

        return (room, player);
    }

    public (GameRoom? room, Player? player) ReplacePlayerWithAi(string connectionId)
    {
        if (!_playerRooms.TryRemove(connectionId, out var code)) return (null, null);
        if (!_rooms.TryGetValue(code, out var room)) return (null, null);

        var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
        if (player == null) return (room, null);

        player.ConnectionId = null;
        player.IsAi = true;

        if (room.Players.All(p => p.IsAi))
        {
            _rooms.TryRemove(code, out _);
        }
        else if (room.HostId == player.Id)
        {
            room.HostId = room.Players.First(p => !p.IsAi).Id;
        }

        return (room, player);
    }

    public Player? ReplaceDisconnectedPlayerWithAi(string roomCode, string playerId)
    {
        if (!_rooms.TryGetValue(roomCode.ToUpper(), out var room)) return null;

        var player = room.Players.FirstOrDefault(p =>
            p.Id == playerId &&
            p.ConnectionId == null &&
            !p.IsAi);
        if (player == null) return null;

        player.IsAi = true;
        if (room.Players.All(p => p.IsAi))
        {
            _rooms.TryRemove(room.Code, out _);
        }
        else if (room.HostId == player.Id)
        {
            room.HostId = room.Players.First(p => !p.IsAi).Id;
        }

        return player;
    }

    private static string GetRandomAvailableAiName(GameRoom room)
    {
        var taken = room.Players
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var available = AiNames
            .Where(name => !taken.Contains(name))
            .ToList();

        if (available.Count > 0)
            return available[Random.Shared.Next(available.Count)];

        return $"Guest {Random.Shared.Next(100, 1000)}";
    }
}
