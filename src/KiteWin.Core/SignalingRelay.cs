using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

/// <summary>
/// Zero-Retention Cloud Signaling Relay Models &amp; Hub
/// Facilitates peer discovery and WebRTC handshake without retaining plain-text architecture payloads.
/// Conforms to Google DeepMind Antigravity Craftsmanship Standard (Zero Emojis).
/// </summary>

public enum SignalingMessageType
{
    Join,
    Joined,
    PeerJoined,
    PeerLeft,
    Offer,
    Answer,
    IceCandidate,
    EncryptedEnvelope,
    Ping,
    Pong
}

public class SignalingPeer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "Windows 11";

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class SignalingMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "join";

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("senderPeerId")]
    public string SenderPeerId { get; set; } = "";

    [JsonPropertyName("targetPeerId")]
    public string? TargetPeerId { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class RelayRoomSession
{
    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("peerCount")]
    public int PeerCount { get; set; } = 1;

    [JsonPropertyName("isEncrypted")]
    public bool IsEncrypted { get; set; } = true;
}

public static class RoomSecurityKeyDeriver
{
    /// <summary>
    /// Derives a 256-bit symmetric key from a shared room secret token using SHA-256
    /// </summary>
    public static byte[] DeriveKey(string secret)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(secret);
        return SHA256.HashData(inputBytes);
    }

    /// <summary>
    /// Generates a high-entropy cryptographically secure room pass-token
    /// </summary>
    public static string GenerateRoomToken(string prefix = "kite")
    {
        byte[] randomBytes = new byte[6];
        RandomNumberGenerator.Fill(randomBytes);
        return $"{prefix}-{Convert.ToHexString(randomBytes).ToLowerInvariant()}";
    }
}

public sealed class RelayStationHub
{
    private static readonly Lazy<RelayStationHub> _instance = new(() => new RelayStationHub());
    public static RelayStationHub Shared => _instance.Value;

    private readonly object _lock = new();
    private readonly Dictionary<string, Dictionary<string, SignalingPeer>> _rooms = new();
    private readonly Dictionary<string, List<SignalingMessage>> _messageQueues = new();

    public List<SignalingPeer> JoinRoom(string roomId, SignalingPeer peer)
    {
        lock (_lock)
        {
            if (!_rooms.TryGetValue(roomId, out var roomPeers))
            {
                roomPeers = new Dictionary<string, SignalingPeer>();
                _rooms[roomId] = roomPeers;
            }

            var existing = new List<SignalingPeer>(roomPeers.Values);
            roomPeers[peer.Id] = peer;

            if (!_messageQueues.ContainsKey(peer.Id))
            {
                _messageQueues[peer.Id] = new List<SignalingMessage>();
            }

            return existing;
        }
    }

    public void LeaveRoom(string roomId, string peerId)
    {
        lock (_lock)
        {
            if (_rooms.TryGetValue(roomId, out var roomPeers))
            {
                roomPeers.Remove(peerId);
                if (roomPeers.Count == 0)
                {
                    _rooms.Remove(roomId);
                }
            }
            _messageQueues.Remove(peerId);
        }
    }

    public void RouteMessage(SignalingMessage message)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(message.TargetPeerId))
            {
                if (_messageQueues.TryGetValue(message.TargetPeerId, out var queue))
                {
                    queue.Add(message);
                }
            }
            else if (_rooms.TryGetValue(message.RoomId, out var roomPeers))
            {
                foreach (var kvp in roomPeers)
                {
                    if (kvp.Key != message.SenderPeerId && _messageQueues.TryGetValue(kvp.Key, out var q))
                    {
                        q.Add(message);
                    }
                }
            }
        }
    }

    public List<SignalingMessage> PopMessages(string peerId)
    {
        lock (_lock)
        {
            if (_messageQueues.TryGetValue(peerId, out var list))
            {
                var copy = new List<SignalingMessage>(list);
                list.Clear();
                return copy;
            }
            return new List<SignalingMessage>();
        }
    }

    public List<SignalingPeer> ActivePeers(string roomId)
    {
        lock (_lock)
        {
            return _rooms.TryGetValue(roomId, out var roomPeers)
                ? new List<SignalingPeer>(roomPeers.Values)
                : new List<SignalingPeer>();
        }
    }

    public List<RelayRoomSession> ActiveRooms()
    {
        lock (_lock)
        {
            var result = new List<RelayRoomSession>();
            foreach (var kvp in _rooms)
            {
                result.Add(new RelayRoomSession
                {
                    RoomId = kvp.Key,
                    PeerCount = kvp.Value.Count,
                    IsEncrypted = true
                });
            }
            return result;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _rooms.Clear();
            _messageQueues.Clear();
        }
    }
}
