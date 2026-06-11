using LoveLetter.Domain.Enums;
using LoveLetter.Domain.Models;

namespace LoveLetter.Services;

using System.Collections.Concurrent;

public class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    // Map connection ID → room code for fast disconnect lookup
    private readonly ConcurrentDictionary<string, string> _playerRooms = new();

    private static readonly Random Rng = new();
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity

    public string GenerateCode()
    {
        string code;
        do { code = new string(Enumerable.Range(0, 6).Select(_ => Chars[Rng.Next(Chars.Length)]).ToArray()); }
        while (_rooms.ContainsKey(code));
        return code;
    }

    public GameRoom CreateRoom(string hostConnectionId, string hostName)
    {
        var code = GenerateCode();
        var room = new GameRoom(code, hostConnectionId);
        var host = new Player(hostConnectionId, hostName);
        room.Players.Add(host);
        _rooms[code] = room;
        _playerRooms[hostConnectionId] = code;
        return room;
    }

    public (GameRoom? room, string? error) JoinRoom(string code, string connectionId, string playerName)
    {
        if (!_rooms.TryGetValue(code.ToUpper(), out var room))
            return (null, "Room not found.");
        if (room.Phase != GamePhase.Lobby)
            return (null, "Game already in progress.");
        if (room.Players.Count >= 6)
            return (null, "Room is full (max 6 players).");
        if (room.Players.Any(p => p.Name == playerName))
            return (null, "Name already taken in this room.");

        room.Players.Add(new Player(connectionId, playerName));
        _playerRooms[connectionId] = code;
        return (room, null);
    }

    public (GameRoom? room, string? error) AddAiPlayer(string code, string requesterId, string aiName = "AI")
    {
        if (!_rooms.TryGetValue(code, out var room))
            return (null, "Room not found.");
        if (room.HostId != requesterId)
            return (null, "Only the host can add AI players.");
        if (room.Players.Count >= 6)
            return (null, "Room is full.");

        var uniqueName = GetUniqueName(room, aiName);
        var ai = new Player($"AI-{Guid.NewGuid():N}", uniqueName, isAi: true);
        room.Players.Add(ai);
        return (room, null);
    }

    public GameRoom? GetRoomByCode(string code) =>
        _rooms.TryGetValue(code.ToUpper(), out var r) ? r : null;

    public GameRoom? GetRoomByPlayer(string connectionId) =>
        _playerRooms.TryGetValue(connectionId, out var code) ? GetRoomByCode(code) : null;

    public void RemovePlayer(string connectionId)
    {
        if (!_playerRooms.TryRemove(connectionId, out var code)) return;
        if (!_rooms.TryGetValue(code, out var room)) return;

        room.Players.RemoveAll(p => p.Id == connectionId);
        if (room.Players.Count == 0)
            _rooms.TryRemove(code, out _);
        else if (room.HostId == connectionId)
            room.HostId = room.Players.First().Id; // Reassign host
    }

    private static string GetUniqueName(GameRoom room, string baseName)
    {
        if (room.Players.All(p => p.Name != baseName)) return baseName;
        int i = 2;
        while (room.Players.Any(p => p.Name == $"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }
}
