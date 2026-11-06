using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiteWin.Core.Sync;

public sealed class KitePeer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("app")]
    public string App { get; set; } = "Kite Windows";

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "Windows";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 24243;

    [JsonPropertyName("lastSeen")]
    public double LastSeenTimestamp { get; set; }

    [JsonIgnore]
    public DateTimeOffset LastSeen
    {
        get => DateTimeOffset.FromUnixTimeSeconds((long)LastSeenTimestamp);
        set => LastSeenTimestamp = value.ToUnixTimeSeconds();
    }
}

public sealed class KiteLANBeacon : IDisposable
{
    public static KiteLANBeacon Shared { get; } = new();
    public const int BeaconPort = 49494;

    private readonly string _instanceId = Guid.NewGuid().ToString();
    private readonly List<KitePeer> _activePeers = [];
    private readonly object _lock = new();

    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _broadcastTask;
    private bool _isRunning;

    public event Action<IReadOnlyList<KitePeer>>? PeersChanged;

    public IReadOnlyList<KitePeer> ActivePeers
    {
        get
        {
            lock (_lock)
            {
                return _activePeers.ToList();
            }
        }
    }

    private KiteLANBeacon() { }

    public void Start(int appPort = 24243, string appName = "Kite Windows", string platform = "Windows")
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
        }

        try
        {
            _listener = new UdpClient();
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.ExclusiveAddressUse = false;
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));

            var token = _cts.Token;
            _listenTask = Task.Run(() => ListenLoopAsync(token), token);
            _broadcastTask = Task.Run(() => BroadcastLoopAsync(appPort, appName, platform, token), token);
        }
        catch
        {
            // UDP broadcast port unavailable or sandboxed
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Dispose();
            _listener = null;
            _activePeers.Clear();
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var result = await _listener.ReceiveAsync(ct);
                var rawJson = Encoding.UTF8.GetString(result.Buffer);

                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("kite_beacon", out var beaconProp) || !beaconProp.GetBoolean())
                    continue;

                var id = root.GetProperty("id").GetString() ?? "";
                if (id == _instanceId || string.IsNullOrEmpty(id))
                    continue; // Skip self

                var app = root.TryGetProperty("app", out var a) ? a.GetString() ?? "Kite" : "Kite";
                var platform = root.TryGetProperty("platform", out var p) ? p.GetString() ?? "Unknown" : "Unknown";
                var hostname = root.TryGetProperty("hostname", out var h) ? h.GetString() ?? "Peer" : "Peer";
                var port = root.TryGetProperty("port", out var pt) ? pt.GetInt32() : 24242;

                var senderIp = result.RemoteEndPoint.Address.ToString();
                if (senderIp == "0.0.0.0" || senderIp == "127.0.0.1")
                    senderIp = "127.0.0.1";

                var now = DateTimeOffset.UtcNow;
                var peer = new KitePeer
                {
                    Id = id,
                    App = app,
                    Platform = platform,
                    Hostname = hostname,
                    Ip = senderIp,
                    Port = port,
                    LastSeen = now
                };

                IReadOnlyList<KitePeer> snapshot;
                lock (_lock)
                {
                    var idx = _activePeers.FindIndex(x => x.Id == id);
                    if (idx >= 0)
                        _activePeers[idx] = peer;
                    else
                        _activePeers.Add(peer);

                    var cutoff = now.AddSeconds(-9.0);
                    _activePeers.RemoveAll(x => x.LastSeen < cutoff);
                    snapshot = _activePeers.ToList();
                }

                PeersChanged?.Invoke(snapshot);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Packet parse failure or socket closed
            }
        }
    }

    private async Task BroadcastLoopAsync(int appPort, string appName, string platform, CancellationToken ct)
    {
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        var endPoint = new IPEndPoint(IPAddress.Broadcast, BeaconPort);

        var hostname = Environment.MachineName;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var payload = new
                {
                    kite_beacon = true,
                    id = _instanceId,
                    app = appName,
                    platform = platform,
                    hostname = hostname,
                    port = appPort,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
                await client.SendAsync(bytes, bytes.Length, endPoint);

                // Prune inactive peers
                var now = DateTimeOffset.UtcNow;
                var cutoff = now.AddSeconds(-9.0);
                IReadOnlyList<KitePeer>? snapshot = null;

                lock (_lock)
                {
                    var before = _activePeers.Count;
                    _activePeers.RemoveAll(x => x.LastSeen < cutoff);
                    if (_activePeers.Count != before)
                        snapshot = _activePeers.ToList();
                }

                if (snapshot != null)
                    PeersChanged?.Invoke(snapshot);

                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Network down or error
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
