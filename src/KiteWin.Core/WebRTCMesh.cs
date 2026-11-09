using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace KiteWin.Core;

/// <summary>
/// Zero-Knowledge WebRTC Mesh &amp; P2P DataChannel Engine
/// Facilitates decentralized, end-to-end encrypted architectural synchronization across WAN networks.
/// Standard: Google DeepMind Antigravity Craftsmanship Standard (Zero Emojis).
/// </summary>

public enum WebRTCPeerState
{
    Disconnected,
    GatheringIce,
    Connecting,
    Connected,
    Failed
}

public class ICECandidatePayload
{
    [JsonPropertyName("candidate")]
    public string Candidate { get; set; } = "";

    [JsonPropertyName("sdpMid")]
    public string? SdpMid { get; set; } = "0";

    [JsonPropertyName("sdpMLineIndex")]
    public int? SdpMLineIndex { get; set; } = 0;
}

public class SDPPayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "offer"; // "offer" or "answer"

    [JsonPropertyName("sdp")]
    public string Sdp { get; set; } = "";
}

public class MeshPeerNode
{
    [JsonPropertyName("id")]
    public string Id => PeerId;

    [JsonPropertyName("peerId")]
    public string PeerId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "Windows 11";

    [JsonPropertyName("state")]
    public string State { get; set; } = "disconnected";

    [JsonPropertyName("latencyMs")]
    public double LatencyMs { get; set; } = 0.0;

    [JsonPropertyName("lastSeen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

public class MeshSyncStatistics
{
    [JsonPropertyName("totalPeers")]
    public int TotalPeers { get; set; }

    [JsonPropertyName("activeConnectedPeers")]
    public int ActiveConnectedPeers { get; set; }

    [JsonPropertyName("averageLatencyMs")]
    public double AverageLatencyMs { get; set; }

    [JsonPropertyName("packetsTransmitted")]
    public int PacketsTransmitted { get; set; }

    [JsonPropertyName("bytesTransmitted")]
    public int BytesTransmitted { get; set; }

    [JsonPropertyName("isEncryptionActive")]
    public bool IsEncryptionActive { get; set; } = true;
}

public sealed class WebRTCMeshManager
{
    private static readonly Lazy<WebRTCMeshManager> _instance = new(() => new WebRTCMeshManager());
    public static WebRTCMeshManager Shared => _instance.Value;

    private readonly object _lock = new();
    public string LocalPeerId { get; }
    public string? CurrentRoomId { get; private set; }
    private byte[]? _roomSymmetricKey;

    private readonly Dictionary<string, MeshPeerNode> _peers = new();
    private int _packetsCount;
    private int _bytesCount;

    public WebRTCMeshManager(string? localPeerId = null)
    {
        LocalPeerId = localPeerId ?? Guid.NewGuid().ToString();
    }

    public void JoinRoom(string roomId, string secretPassphrase)
    {
        lock (_lock)
        {
            CurrentRoomId = roomId;
            _roomSymmetricKey = RoomSecurityKeyDeriver.DeriveKey(secretPassphrase);
            _peers.Clear();
        }
    }

    public void RegisterRemotePeer(string peerId, string displayName, string platform)
    {
        lock (_lock)
        {
            _peers[peerId] = new MeshPeerNode
            {
                PeerId = peerId,
                DisplayName = displayName,
                Platform = platform,
                State = "connecting",
                LatencyMs = 12.0,
                LastSeen = DateTime.UtcNow
            };
        }
    }

    public void SetPeerState(string peerId, WebRTCPeerState state, double? latencyMs = null)
    {
        lock (_lock)
        {
            if (_peers.TryGetValue(peerId, out var node))
            {
                node.State = state.ToString().ToLowerInvariant();
                if (latencyMs.HasValue)
                {
                    node.LatencyMs = latencyMs.Value;
                }
                node.LastSeen = DateTime.UtcNow;
            }
        }
    }

    public void RemovePeer(string peerId)
    {
        lock (_lock)
        {
            _peers.Remove(peerId);
        }
    }

    public EncryptedSyncEnvelope PackEncryptedMessage(byte[] payloadBytes)
    {
        lock (_lock)
        {
            if (_roomSymmetricKey == null)
            {
                throw new InvalidOperationException("No active room encryption key configured.");
            }

            var envelope = EncryptedSyncEnvelope.Encrypt(payloadBytes, _roomSymmetricKey, LocalPeerId);
            _packetsCount++;
            _bytesCount += payloadBytes.Length;
            return envelope;
        }
    }

    public byte[] UnpackEncryptedMessage(EncryptedSyncEnvelope envelope)
    {
        lock (_lock)
        {
            if (_roomSymmetricKey == null)
            {
                throw new InvalidOperationException("No active room encryption key configured.");
            }

            return envelope.Decrypt(_roomSymmetricKey);
        }
    }

    public MeshSyncStatistics Statistics()
    {
        lock (_lock)
        {
            var activeCount = _peers.Values.Count(p => p.State == "connected");
            var totalCount = _peers.Count;
            var latencies = _peers.Values.Select(p => p.LatencyMs).ToList();
            var avgLat = latencies.Count == 0 ? 0.0 : latencies.Average();

            return new MeshSyncStatistics
            {
                TotalPeers = totalCount,
                ActiveConnectedPeers = activeCount,
                AverageLatencyMs = Math.Round(avgLat, 2),
                PacketsTransmitted = _packetsCount,
                BytesTransmitted = _bytesCount,
                IsEncryptionActive = _roomSymmetricKey != null
            };
        }
    }

    public List<MeshPeerNode> ActivePeers()
    {
        lock (_lock)
        {
            return new List<MeshPeerNode>(_peers.Values);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            CurrentRoomId = null;
            _roomSymmetricKey = null;
            _peers.Clear();
            _packetsCount = 0;
            _bytesCount = 0;
        }
    }
}
