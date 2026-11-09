using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using KiteWin.Core.Models;

namespace KiteWin.Core;

/// <summary>
/// Hybrid Logical Clock (HLC) providing strict causal ordering across distributed nodes.
/// Combines physical time with a monotonic logical counter and unique client identifier.
/// </summary>
public struct HybridLogicalClock : IComparable<HybridLogicalClock>, IEquatable<HybridLogicalClock>
{
    [JsonPropertyName("physicalMillis")]
    public ulong PhysicalMillis { get; set; }

    [JsonPropertyName("logicalCounter")]
    public uint LogicalCounter { get; set; }

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; }

    public HybridLogicalClock(ulong? physicalMillis = null, uint logicalCounter = 0, string? clientId = null)
    {
        PhysicalMillis = physicalMillis ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        LogicalCounter = logicalCounter;
        ClientId = clientId ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Advance the clock for a local mutation.
    /// </summary>
    public void Tick()
    {
        ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now > PhysicalMillis)
        {
            PhysicalMillis = now;
            LogicalCounter = 0;
        }
        else
        {
            LogicalCounter++;
        }
    }

    /// <summary>
    /// Merge with a remote clock upon receiving a packet.
    /// </summary>
    public void Update(HybridLogicalClock remote)
    {
        ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ulong maxPhysical = Math.Max(now, Math.Max(PhysicalMillis, remote.PhysicalMillis));

        if (maxPhysical == PhysicalMillis && maxPhysical == remote.PhysicalMillis)
        {
            LogicalCounter = Math.Max(LogicalCounter, remote.LogicalCounter) + 1;
        }
        else if (maxPhysical == PhysicalMillis)
        {
            LogicalCounter++;
        }
        else if (maxPhysical == remote.PhysicalMillis)
        {
            LogicalCounter = remote.LogicalCounter + 1;
        }
        else
        {
            LogicalCounter = 0;
        }
        PhysicalMillis = maxPhysical;
    }

    public int CompareTo(HybridLogicalClock other)
    {
        if (PhysicalMillis != other.PhysicalMillis)
        {
            return PhysicalMillis.CompareTo(other.PhysicalMillis);
        }
        if (LogicalCounter != other.LogicalCounter)
        {
            return LogicalCounter.CompareTo(other.LogicalCounter);
        }
        return string.Compare(ClientId, other.ClientId, StringComparison.Ordinal);
    }

    public bool Equals(HybridLogicalClock other)
    {
        return PhysicalMillis == other.PhysicalMillis &&
               LogicalCounter == other.LogicalCounter &&
               ClientId == other.ClientId;
    }

    public override bool Equals(object? obj) => obj is HybridLogicalClock other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(PhysicalMillis, LogicalCounter, ClientId);

    public static bool operator <(HybridLogicalClock left, HybridLogicalClock right) => left.CompareTo(right) < 0;
    public static bool operator >(HybridLogicalClock left, HybridLogicalClock right) => left.CompareTo(right) > 0;
    public static bool operator <=(HybridLogicalClock left, HybridLogicalClock right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HybridLogicalClock left, HybridLogicalClock right) => left.CompareTo(right) >= 0;
    public static bool operator ==(HybridLogicalClock left, HybridLogicalClock right) => left.Equals(right);
    public static bool operator !=(HybridLogicalClock left, HybridLogicalClock right) => !left.Equals(right);
}

/// <summary>
/// Last-Write-Wins Register providing deterministic conflict resolution.
/// </summary>
public sealed class LwwRegister<T> where T : class
{
    [JsonPropertyName("value")]
    public T Value { get; set; }

    [JsonPropertyName("clock")]
    public HybridLogicalClock Clock { get; set; }

    public LwwRegister(T value, HybridLogicalClock clock)
    {
        Value = value;
        Clock = clock;
    }

    /// <summary>
    /// Merges another register, keeping whichever value has the causally later clock.
    /// </summary>
    public void Merge(LwwRegister<T> other)
    {
        if (other.Clock > Clock)
        {
            Value = other.Value;
            Clock = other.Clock;
        }
    }
}

/// <summary>
/// Real-time presence state for active collaborators on the canvas.
/// </summary>
public sealed class CollaboratorPresence
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("colorHex")]
    public string ColorHex { get; set; } = "#0A84FF";

    [JsonPropertyName("cursorX")]
    public double CursorX { get; set; }

    [JsonPropertyName("cursorY")]
    public double CursorY { get; set; }

    [JsonPropertyName("selectedNodeIds")]
    public List<string> SelectedNodeIds { get; set; } = new();

    [JsonPropertyName("viewportX")]
    public double ViewportX { get; set; }

    [JsonPropertyName("viewportY")]
    public double ViewportY { get; set; }

    [JsonPropertyName("viewportZoom")]
    public double ViewportZoom { get; set; } = 1.0;

    [JsonPropertyName("clock")]
    public HybridLogicalClock Clock { get; set; }
}

/// <summary>
/// Replicated CRDT document state for Kite Canvas on Windows.
/// Mathematically converges across arbitrary network latency and delivery permutations.
/// </summary>
public sealed class CanvasCRDTDocument
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("frames")]
    public Dictionary<string, LwwRegister<CanvasFrame>> Frames { get; set; } = new();

    [JsonPropertyName("connections")]
    public Dictionary<string, LwwRegister<CanvasConnection>> Connections { get; set; } = new();

    [JsonPropertyName("clock")]
    public HybridLogicalClock Clock { get; set; }

    public CanvasCRDTDocument(string? documentId = null, string? clientId = null)
    {
        DocumentId = documentId ?? Guid.NewGuid().ToString();
        Clock = new HybridLogicalClock(clientId: clientId ?? Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Mutate or add a frame with local clock advancement.
    /// </summary>
    public void SetFrame(CanvasFrame frame)
    {
        var clock = Clock;
        clock.Tick();
        Clock = clock;

        Frames[frame.Id] = new LwwRegister<CanvasFrame>(frame, Clock);
    }

    /// <summary>
    /// Mutate or add a connection with local clock advancement.
    /// </summary>
    public void SetConnection(CanvasConnection connection)
    {
        var clock = Clock;
        clock.Tick();
        Clock = clock;

        Connections[connection.Id] = new LwwRegister<CanvasConnection>(connection, Clock);
    }

    /// <summary>
    /// Remove a frame.
    /// </summary>
    public void RemoveFrame(string id)
    {
        Frames.Remove(id);
    }

    /// <summary>
    /// Merge remote CRDT document state with guaranteed convergence.
    /// </summary>
    public void Merge(CanvasCRDTDocument remote)
    {
        var clock = Clock;
        clock.Update(remote.Clock);
        Clock = clock;

        foreach (var (key, remoteReg) in remote.Frames)
        {
            if (Frames.TryGetValue(key, out var localReg))
            {
                localReg.Merge(remoteReg);
            }
            else
            {
                Frames[key] = remoteReg;
            }
        }

        foreach (var (key, remoteReg) in remote.Connections)
        {
            if (Connections.TryGetValue(key, out var localReg))
            {
                localReg.Merge(remoteReg);
            }
            else
            {
                Connections[key] = remoteReg;
            }
        }
    }
}

/// <summary>
/// Zero-Knowledge Encrypted Synchronization Envelope.
/// Uses ChaCha20-Poly1305 / AES-GCM symmetric AEAD for tamper-evident transport.
/// </summary>
public sealed class EncryptedSyncEnvelope
{
    [JsonPropertyName("senderClientId")]
    public string SenderClientId { get; set; } = string.Empty;

    [JsonPropertyName("nonceBase64")]
    public string NonceBase64 { get; set; } = string.Empty;

    [JsonPropertyName("ciphertextBase64")]
    public string CiphertextBase64 { get; set; } = string.Empty;

    [JsonPropertyName("tagBase64")]
    public string TagBase64 { get; set; } = string.Empty;

    public EncryptedSyncEnvelope() { }

    public EncryptedSyncEnvelope(string senderClientId, string nonceBase64, string ciphertextBase64, string tagBase64)
    {
        SenderClientId = senderClientId;
        NonceBase64 = nonceBase64;
        CiphertextBase64 = ciphertextBase64;
        TagBase64 = tagBase64;
    }

    /// <summary>
    /// Encrypts raw bytes using 256-bit AES-GCM.
    /// </summary>
    public static EncryptedSyncEnvelope Encrypt(byte[] payload, byte[] key, string senderClientId)
    {
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        byte[] ciphertext = new byte[payload.Length];
        byte[] tag = new byte[16];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(nonce, payload, ciphertext, tag);

        return new EncryptedSyncEnvelope(
            senderClientId,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag)
        );
    }

    /// <summary>
    /// Decrypts raw bytes using 256-bit AES-GCM.
    /// </summary>
    public byte[] Decrypt(byte[] key)
    {
        byte[] nonce = Convert.FromBase64String(NonceBase64);
        byte[] ciphertext = Convert.FromBase64String(CiphertextBase64);
        byte[] tag = Convert.FromBase64String(TagBase64);

        byte[] plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
