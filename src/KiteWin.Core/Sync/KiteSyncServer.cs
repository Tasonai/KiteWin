using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using KiteWin.Core.Models;

namespace KiteWin.Core.Sync;

/// <summary>
/// Embedded HTTP Server for Windows that serves Kite REST API on port 24243.
/// Allows Kite macOS, Web Studio, and other agents to query and sync ideas over LAN.
/// </summary>
public sealed class KiteSyncServer : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly ItemStore _store;
    private readonly CanvasStore? _canvasStore;
    private readonly List<HttpListenerResponse> _sseClients = [];
    private readonly object _sseLock = new();

    public int Port { get; private set; }
    public bool IsRunning => _listener?.IsListening == true;

    public KiteSyncServer(ItemStore store, CanvasStore? canvasStore = null, int port = 24243)
    {
        _store = store;
        _canvasStore = canvasStore;
        Port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            KiteLANBeacon.Shared.Start(Port, "Kite Windows", "Windows");
        }
        catch (HttpListenerException)
        {
            // Port might be restricted without admin; fallback to localhost
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
                KiteLANBeacon.Shared.Start(Port, "Kite Windows", "Windows");
            }
            catch
            {
                // Fallback failed
                _listener = null;
            }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        KiteLANBeacon.Shared.Stop();

        try
        {
            lock (_sseLock)
            {
                foreach (var client in _sseClients)
                {
                    try { client.Close(); } catch { }
                }
                _sseClients.Clear();
            }

            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Ignore shutdown errors
        }
        finally
        {
            _listener = null;
            _cts = null;
        }
    }

    public void Broadcast(string eventType, object? payload = null)
    {
        var json = JsonSerializer.Serialize(new { type = eventType, payload });
        var msg = $"event: message\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(msg);

        lock (_sseLock)
        {
            for (var i = _sseClients.Count - 1; i >= 0; i--)
            {
                var client = _sseClients[i];
                try
                {
                    client.OutputStream.Write(bytes);
                    client.OutputStream.Flush();
                }
                catch
                {
                    _sseClients.RemoveAt(i);
                }
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Ignore transient errors
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // Apply CORS headers for Web Studio / Cross-origin callers
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");

        if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            res.StatusCode = (int)HttpStatusCode.NoContent;
            res.Close();
            return;
        }

        try
        {
            var path = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "";

            switch (path)
            {
                case "" or "/" or "/index.html":
                    var html = WebStudio.GetHtml(Port, "Kite Windows (BAWA)");
                    res.ContentType = "text/html; charset=utf-8";
                    res.StatusCode = (int)HttpStatusCode.OK;
                    var htmlBytes = Encoding.UTF8.GetBytes(html);
                    await res.OutputStream.WriteAsync(htmlBytes);
                    res.Close();
                    break;

                case "/api/events":
                    res.ContentType = "text/event-stream; charset=utf-8";
                    res.Headers.Add("Cache-Control", "no-cache");
                    res.Headers.Add("Connection", "keep-alive");
                    res.StatusCode = (int)HttpStatusCode.OK;
                    lock (_sseLock) { _sseClients.Add(res); }
                    var welcomeBytes = Encoding.UTF8.GetBytes($"data: {{\"type\":\"connected\",\"app\":\"Kite Windows\",\"port\":{Port}}}\n\n");
                    await res.OutputStream.WriteAsync(welcomeBytes);
                    await res.OutputStream.FlushAsync();
                    return;

                case "/api/status":
                    var status = new KiteStatusResponse
                    {
                        Status = "online",
                        App = "Kite Windows (BAWA)",
                        Version = "1.0",
                        Port = Port
                    };
                    await WriteJsonResponseAsync(res, status);
                    break;

                case "/api/peers":
                    var peers = KiteLANBeacon.Shared.ActivePeers;
                    await WriteJsonResponseAsync(res, peers);
                    break;

                case "/api/ideas":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var items = _store.All();
                        var payload = new KiteIdeasPayload
                        {
                            Version = 1,
                            ExportedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
                            Ideas = items.Select(KiteSyncClient.ItemToDto).ToList()
                        };
                        await WriteJsonResponseAsync(res, payload);
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var payload = JsonSerializer.Deserialize<KiteIdeasPayload>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        var count = 0;
                        if (payload?.Ideas != null)
                        {
                            foreach (var dto in payload.Ideas)
                            {
                                var item = KiteSyncClient.DtoToItem(dto);
                                _store.Save(item);
                                count++;
                            }
                        }

                        var responseObj = new { status = "ok", imported = count };
                        Broadcast("ideas_updated", new { imported = count });
                        await WriteJsonResponseAsync(res, responseObj);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/canvas":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var json = _canvasStore?.GetSnapshotJson() ?? "{}";
                        res.ContentType = "application/json; charset=utf-8";
                        res.StatusCode = (int)HttpStatusCode.OK;
                        var bytes = Encoding.UTF8.GetBytes(json);
                        await res.OutputStream.WriteAsync(bytes);
                        res.Close();
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var success = _canvasStore?.ImportSnapshot(json) ?? false;
                        if (success) Broadcast("canvas_updated", new { success = true });
                        var responseObj = new { status = success ? "ok" : "error" };
                        await WriteJsonResponseAsync(res, responseObj);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/active":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var filePath = root.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                        int? line = root.TryGetProperty("line", out var l) ? l.GetInt32() : null;
                        var editor = root.TryGetProperty("editor", out var ed) ? ed.GetString() ?? "ide" : "ide";

                        IDEBridgeHub.Shared.UpdateActiveFile(filePath, line);
                        Broadcast("ide_active_file", new { filePath, line = line ?? 0, editor });
                        await WriteJsonResponseAsync(res, new { status = "ok", filePath });
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/navigate":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var filePath = root.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                        var line = root.TryGetProperty("line", out var l) ? l.GetInt32() : 1;
                        var column = root.TryGetProperty("column", out var c) ? c.GetInt32() : 1;
                        var editor = root.TryGetProperty("editor", out var ed) ? ed.GetString() ?? "vscode" : "vscode";

                        Broadcast("ide_navigate", new { filePath, line, column, editor });
                        await WriteJsonResponseAsync(res, new { status = "ok", navigated = true });
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/ide/status":
                    var (activePath, activeLine) = IDEBridgeHub.Shared.CurrentActiveFile;
                    await WriteJsonResponseAsync(res, new
                    {
                        status = "online",
                        activeFilePath = activePath ?? "",
                        activeLine = activeLine ?? 0
                    });
                    break;

                case "/api/telemetry":
                    if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        var all = IDEBridgeHub.Shared.AllTelemetries();
                        await WriteJsonResponseAsync(res, all);
                    }
                    else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var telemetry = JsonSerializer.Deserialize<NodeTelemetry>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (telemetry != null && !string.IsNullOrEmpty(telemetry.NodeId))
                        {
                            IDEBridgeHub.Shared.SetTelemetry(telemetry);
                            Broadcast("telemetry_updated", new
                            {
                                nodeId = telemetry.NodeId,
                                status = telemetry.Status,
                                rps = telemetry.Rps,
                                p99LatencyMs = telemetry.P99LatencyMs,
                                errorRate = telemetry.ErrorRate
                            });
                            await WriteJsonResponseAsync(res, telemetry);
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.BadRequest;
                            res.Close();
                        }
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/relay/create":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var roomToken = RoomSecurityKeyDeriver.GenerateRoomToken();
                        var resPayload = new
                        {
                            status = "ok",
                            roomId = roomToken,
                            joinUrl = $"kite://join?room={roomToken}",
                            isEncrypted = true
                        };
                        await WriteJsonResponseAsync(res, resPayload);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/relay/status":
                    var stats = WebRTCMeshManager.Shared.Statistics();
                    var meshPeers = WebRTCMeshManager.Shared.ActivePeers();
                    await WriteJsonResponseAsync(res, new
                    {
                        stats,
                        peers = meshPeers,
                        isOnline = true
                    });
                    break;

                case "/api/relay/signal":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var sigMsg = JsonSerializer.Deserialize<SignalingMessage>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (sigMsg != null)
                        {
                            RelayStationHub.Shared.RouteMessage(sigMsg);
                            Broadcast("relay_signal_routed", new { type = sigMsg.Type, roomId = sigMsg.RoomId, sender = sigMsg.SenderPeerId });
                            await WriteJsonResponseAsync(res, new { status = "ok", routed = true });
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.BadRequest;
                            res.Close();
                        }
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case string s when s.StartsWith("/api/relay/messages"):
                    var queryPeerId = req.QueryString["peerId"] ?? "";
                    var msgs = RelayStationHub.Shared.PopMessages(queryPeerId);
                    await WriteJsonResponseAsync(res, msgs);
                    break;

                case "/api/plugins":
                    var plugins = WasmPluginEngine.Shared.AllPlugins();
                    await WriteJsonResponseAsync(res, plugins);
                    break;

                case "/api/plugins/execute":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var pluginId = root.TryGetProperty("pluginId", out var pId) ? pId.GetString() ?? "" : "";
                        var context = new Dictionary<string, object>();
                        if (root.TryGetProperty("context", out var ctxElem) && ctxElem.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in ctxElem.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                    context[prop.Name] = prop.Value.GetString() ?? "";
                                else if (prop.Value.ValueKind == JsonValueKind.Object)
                                {
                                    var inner = new Dictionary<string, string>();
                                    foreach (var sub in prop.Value.EnumerateObject())
                                        inner[sub.Name] = sub.Value.GetString() ?? "";
                                    context[prop.Name] = inner;
                                }
                            }
                        }
                        var execResult = WasmPluginEngine.Shared.ExecutePlugin(pluginId, context);
                        await WriteJsonResponseAsync(res, execResult);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/worktree/status":
                    var session = WorktreeSyncEngine.Instance.GetSession();
                    await WriteJsonResponseAsync(res, session ?? new WorktreeSession());
                    break;

                case "/api/worktree/sync":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string? branch = null;
                        var defaultNodes = new List<(string Id, string Name, string Type)>
                        {
                            ("node-1", "Orders API", "api-service"),
                            ("node-2", "Orders DB", "postgres-database"),
                            ("node-3", "Order Events", "kafka-stream")
                        };
                        try
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                            var json = await reader.ReadToEndAsync();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("branch", out var bProp))
                                {
                                    branch = bProp.GetString();
                                }
                            }
                        }
                        catch { }

                        var syncRes = WorktreeSyncEngine.Instance.ExecuteSync(branchName: branch, nodes: defaultNodes);
                        await WriteJsonResponseAsync(res, syncRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/worktree/pr":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string? customTitle = null;
                        try
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                            var json = await reader.ReadToEndAsync();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("title", out var tProp))
                                {
                                    customTitle = tProp.GetString();
                                }
                            }
                        }
                        catch { }

                        var prRes = WorktreeSyncEngine.Instance.SynthesizePullRequest(customTitle: customTitle);
                        await WriteJsonResponseAsync(res, prRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/audit/trail":
                    var leaves = MerkleAuditEngine.Instance.AllLeaves();
                    var root = MerkleAuditEngine.Instance.ComputeMerkleRoot();
                    var auditPayload = new
                    {
                        rootHash = root.RootHash,
                        leafCount = root.LeafCount,
                        height = root.Height,
                        generatedAt = root.GeneratedAtIso,
                        leaves = leaves.Select(l => new
                        {
                            index = l.Index,
                            action = l.Action,
                            actor = l.Actor.Email,
                            entityId = l.EntityId,
                            leafHash = l.LeafHash,
                            previousHash = l.PreviousHash
                        }).ToList()
                    };
                    await WriteJsonResponseAsync(res, auditPayload);
                    break;

                case "/api/audit/record":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var rootElem = doc.RootElement;
                        var act = rootElem.TryGetProperty("action", out var aProp) ? aProp.GetString() ?? "nodeUpdated" : "nodeUpdated";
                        var entId = rootElem.TryGetProperty("entityId", out var eProp) ? eProp.GetString() ?? "entity-1" : "entity-1";
                        var entType = rootElem.TryGetProperty("entityType", out var etProp) ? etProp.GetString() ?? "node" : "node";
                        var payload = rootElem.TryGetProperty("payload", out var pProp) ? pProp.GetString() ?? "{}" : "{}";

                        var leaf = MerkleAuditEngine.Instance.RecordAction(act, entId, entType, payload);
                        await WriteJsonResponseAsync(res, leaf);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/audit/verify":
                    int leafIdx = 0;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                            var json = await reader.ReadToEndAsync();
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                using var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("leafIndex", out var idxProp))
                                {
                                    leafIdx = idxProp.GetInt32();
                                }
                            }
                        }
                        catch { }
                    }
                    var proof = MerkleAuditEngine.Instance.GenerateInclusionProof(leafIdx);
                    if (proof != null)
                    {
                        await WriteJsonResponseAsync(res, proof);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.NotFound;
                        res.Close();
                    }
                    break;

                case "/api/iam/session":
                    var currentActor = MerkleAuditEngine.Instance.GetCurrentActor();
                    await WriteJsonResponseAsync(res, currentActor);
                    break;

                case "/api/audit/rfc":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(json);
                        var rootElem = doc.RootElement;
                        var rfcId = rootElem.TryGetProperty("rfcId", out var rProp) ? rProp.GetString() ?? "" : "";
                        var approve = rootElem.TryGetProperty("approve", out var apProp) && apProp.GetBoolean();

                        var (success, error, item) = MerkleAuditEngine.Instance.SignOffRFC(rfcId, approve);
                        if (success && item != null)
                        {
                            await WriteJsonResponseAsync(res, item);
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.Forbidden;
                            var errBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error }));
                            res.ContentType = "application/json; charset=utf-8";
                            await res.OutputStream.WriteAsync(errBytes);
                            res.Close();
                        }
                    }
                    else
                    {
                        var allRfcs = MerkleAuditEngine.Instance.AllRFCs();
                        await WriteJsonResponseAsync(res, allRfcs);
                    }
                    break;

                case "/api/tf/export":
                    var sampleTfNodes = new List<(string Id, string Name, string Type)>
                    {
                        ("node-1", "Billing Database", "postgres-database"),
                        ("node-2", "Payment Processor", "api-service"),
                        ("node-3", "Invoices Bucket", "s3-storage")
                    };
                    var tfBundle = TerraformIaCEngine.Instance.SynthesizeHcl("aws", sampleTfNodes);
                    await WriteJsonResponseAsync(res, tfBundle);
                    break;

                case "/api/tf/import":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var stateStr = "{}";
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("jsonState", out var sProp))
                                stateStr = sProp.GetString() ?? "{}";
                            else
                                stateStr = json;
                        }
                        catch { stateStr = json; }

                        var importRes = TerraformIaCEngine.Instance.ReverseEngineerState(stateStr);
                        await WriteJsonResponseAsync(res, importRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/tf/plan":
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        var activeList = new List<string>();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("activeResources", out var aProp) && aProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in aProp.EnumerateArray())
                                    activeList.Add(item.GetString() ?? "");
                            }
                        }
                        catch { }

                        var targetTfNodes = new List<(string Id, string Name, string Type)>
                        {
                            ("node-1", "Billing Database", "postgres-database"),
                            ("node-2", "Payment Processor", "api-service"),
                            ("node-3", "Invoices Bucket", "s3-storage")
                        };
                        var planRes = TerraformIaCEngine.Instance.CalculatePlan(targetTfNodes, activeList);
                        await WriteJsonResponseAsync(res, planRes);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
                    break;

                case "/api/security/audit":
                    var sampleSecNodes = new List<(string Id, string Name, string Type)>
                    {
                        ("node-1", "Customer Portal", "web-frontend"),
                        ("node-2", "Orders DB", "postgres-database"),
                        ("node-3", "Order Events", "kafka-stream")
                    };
                    var sampleSecConns = new List<(string FromId, string ToId, string ProtocolType)>
                    {
                        ("node-1", "node-2", "postgresql")
                    };
                    var scorecard = SecurityChaosEngine.Instance.AuditTopology(sampleSecNodes, sampleSecConns);
                    await WriteJsonResponseAsync(res, scorecard);
                    break;

                case "/api/security/blast-radius":
                    var targetId = "node-2";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("targetNodeId", out var tidProp))
                                targetId = tidProp.GetString() ?? "node-2";
                        }
                        catch { }
                    }
                    var blastNodes = new List<(string Id, string Name, string Type)>
                    {
                        ("node-1", "Customer Portal", "web-frontend"),
                        ("node-2", "API Gateway", "api-gateway"),
                        ("node-3", "Orders DB", "postgres-database")
                    };
                    var blastConns = new List<(string FromId, string ToId, string ProtocolType)>
                    {
                        ("node-1", "node-2", "https"),
                        ("node-2", "node-3", "postgresql")
                    };
                    var blastReport = SecurityChaosEngine.Instance.CalculateBlastRadius(targetId, blastNodes, blastConns);
                    await WriteJsonResponseAsync(res, blastReport);
                    break;

                case "/api/chaos/generate":
                    var chaosType = "podKill";
                    var chaosTarget = "orders-service";
                    var chaosDuration = 60;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("type", out var tProp)) chaosType = tProp.GetString() ?? "podKill";
                            if (root.TryGetProperty("targetNodeName", out var tnProp)) chaosTarget = tnProp.GetString() ?? "orders-service";
                            if (root.TryGetProperty("durationSec", out var dProp)) chaosDuration = dProp.GetInt32();
                        }
                        catch { }
                    }
                    var chaosSpec = SecurityChaosEngine.Instance.GenerateChaosExperiment(chaosType, chaosTarget, chaosDuration);
                    await WriteJsonResponseAsync(res, chaosSpec);
                    break;

                case "/api/local-ai/status":
                    var status = LocalAiCoPilotEngine.Instance.GetEngineStatus();
                    await WriteJsonResponseAsync(res, status);
                    break;

                case "/api/local-ai/critique":
                    var sampleNodes = new List<(string Id, string Name, string Type)>
                    {
                        ("node-1", "Checkout Service", "api-service"),
                        ("node-2", "Inventory Service", "api-service"),
                        ("node-3", "Orders DB", "postgres-database")
                    };
                    var sampleConns = new List<(string FromId, string ToId, string ProtocolType)>
                    {
                        ("node-1", "node-3", "postgresql"),
                        ("node-2", "node-3", "postgresql"),
                        ("node-1", "node-2", "http"),
                        ("node-2", "node-1", "http")
                    };
                    var critiqueRes = LocalAiCoPilotEngine.Instance.AnalyzeTopology(sampleNodes, sampleConns);
                    await WriteJsonResponseAsync(res, critiqueRes);
                    break;

                case "/api/local-ai/chat":
                    var prompt = "How do I make my architecture resilient?";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("prompt", out var pProp))
                                prompt = pProp.GetString() ?? prompt;
                        }
                        catch { }
                    }
                    var chatRes = LocalAiCoPilotEngine.Instance.ChatLocal(prompt);
                    await WriteJsonResponseAsync(res, chatRes);
                    break;

                case "/api/timetravel/timeline":
                    var timeline = TimeTravelReplayEngine.Instance.GetTimeline();
                    var incidents = TimeTravelReplayEngine.Instance.GetIncidents();
                    var timelinePayload = new
                    {
                        snapshots = timeline.Select(s => new
                        {
                            id = s.Id,
                            timestamp = s.Timestamp,
                            author = s.Author,
                            message = s.Message,
                            nodeCount = s.NodeCount,
                            connectorCount = s.ConnectorCount,
                            incidentIds = s.IncidentIds
                        }).ToList(),
                        incidents = incidents.Select(i => new
                        {
                            id = i.Id,
                            title = i.Title,
                            severity = i.Severity,
                            service = i.Service,
                            timestamp = i.Timestamp,
                            status = i.Status,
                            mttrMinutes = i.MttrMinutes,
                            suspectedSnapshotId = i.SuspectedSnapshotId
                        }).ToList()
                    };
                    await WriteJsonResponseAsync(res, timelinePayload);
                    break;

                case "/api/timetravel/snapshot":
                    var snapshotId = "snap-v1.0.0";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("snapshotId", out var sProp))
                                snapshotId = sProp.GetString() ?? snapshotId;
                        }
                        catch { }
                    }
                    var snapshot = TimeTravelReplayEngine.Instance.GetSnapshot(snapshotId);
                    if (snapshot != null)
                    {
                        await WriteJsonResponseAsync(res, snapshot);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.NotFound;
                        res.Close();
                    }
                    break;

                case "/api/timetravel/diff":
                    var fromId = "snap-v1.0.0";
                    var toId = "snap-v1.3.0";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("fromId", out var fProp))
                                fromId = fProp.GetString() ?? fromId;
                            if (doc.RootElement.TryGetProperty("toId", out var tProp))
                                toId = tProp.GetString() ?? toId;
                        }
                        catch { }
                    }
                    var diffResult = TimeTravelReplayEngine.Instance.ComputeDiff(fromId, toId);
                    await WriteJsonResponseAsync(res, diffResult);
                    break;

                case "/api/timetravel/correlate":
                    var incId = "INC-7102";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("incidentId", out var iProp))
                                incId = iProp.GetString() ?? incId;
                        }
                        catch { }
                    }
                    var correlationResult = TimeTravelReplayEngine.Instance.CorrelateIncident(incId);
                    await WriteJsonResponseAsync(res, correlationResult);
                    break;

                case "/api/cluster/status":
                    var clusterStatus = AirGappedClusterEngine.Instance.GetClusterStatus();
                    await WriteJsonResponseAsync(res, clusterStatus);
                    break;

                case "/api/cluster/elect":
                    string? candidateId = null;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("candidateId", out var cProp))
                                candidateId = cProp.GetString();
                        }
                        catch { }
                    }
                    var electResult = AirGappedClusterEngine.Instance.TriggerLeaderElection(candidateId);
                    await WriteJsonResponseAsync(res, electResult);
                    break;

                case "/api/cluster/manifest":
                    int replicas = 3;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("replicas", out var rProp) && rProp.TryGetInt32(out var rVal))
                                replicas = rVal;
                        }
                        catch { }
                    }
                    var k8sManifest = AirGappedClusterEngine.Instance.GenerateOperatorManifest(replicas);
                    await WriteJsonResponseAsync(res, k8sManifest);
                    break;

                case "/api/ebpf/flows":
                    var ebpfFlows = EbpfTelemetryEngine.Instance.GetKernelFlows();
                    await WriteJsonResponseAsync(res, new { flows = ebpfFlows });
                    break;

                case "/api/ebpf/shadow-audit":
                    List<DeclaredCanvasEdge>? declaredEdges = null;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("declaredEdges", out var edgesProp) && edgesProp.ValueKind == JsonValueKind.Array)
                            {
                                declaredEdges = new List<DeclaredCanvasEdge>();
                                foreach (var elem in edgesProp.EnumerateArray())
                                {
                                    var src = elem.TryGetProperty("source", out var sp) ? sp.GetString() : null;
                                    var dst = elem.TryGetProperty("destination", out var dp) ? dp.GetString() : null;
                                    if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(dst))
                                        declaredEdges.Add(new DeclaredCanvasEdge { Source = src, Destination = dst });
                                }
                            }
                        }
                        catch { }
                    }
                    var shadowReport = EbpfTelemetryEngine.Instance.AuditShadowDependencies(declaredEdges);
                    await WriteJsonResponseAsync(res, shadowReport);
                    break;

                case "/api/ebpf/policy":
                    string ebpfNs = "kite-prod";
                    bool blockRogue = true;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("namespace", out var nsProp))
                                ebpfNs = nsProp.GetString() ?? ebpfNs;
                            if (doc.RootElement.TryGetProperty("blockRogueEgress", out var brProp) && (brProp.ValueKind == JsonValueKind.True || brProp.ValueKind == JsonValueKind.False))
                                blockRogue = brProp.GetBoolean();
                        }
                        catch { }
                    }
                    var ebpfBundle = EbpfTelemetryEngine.Instance.GenerateEbpfPolicy(ebpfNs, blockRogue);
                    await WriteJsonResponseAsync(res, ebpfBundle);
                    break;

                case "/api/edge-mesh/pops":
                    var pops = WasmEdgeMeshEngine.Instance.GetGlobalPops();
                    await WriteJsonResponseAsync(res, new { pops });
                    break;

                case "/api/edge-mesh/route":
                    double clientLat = 52.5200;
                    double clientLon = 13.4050;
                    string consistencyTier = "bounded-staleness-50ms";
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("latitude", out var latProp) && latProp.TryGetDouble(out var latVal))
                                clientLat = latVal;
                            if (doc.RootElement.TryGetProperty("longitude", out var lonProp) && lonProp.TryGetDouble(out var lonVal))
                                clientLon = lonVal;
                            if (doc.RootElement.TryGetProperty("consistencyTier", out var cProp))
                                consistencyTier = cProp.GetString() ?? consistencyTier;
                        }
                        catch { }
                    }
                    var routeDecision = WasmEdgeMeshEngine.Instance.ComputeOptimalRoute(clientLat, clientLon, consistencyTier);
                    await WriteJsonResponseAsync(res, routeDecision);
                    break;

                case "/api/edge-mesh/bundle":
                    string workerName = "kite-auth-edge-gate";
                    int memLimitMb = 64;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("workerName", out var wProp))
                                workerName = wProp.GetString() ?? workerName;
                            if (doc.RootElement.TryGetProperty("memoryLimitMb", out var mProp) && mProp.TryGetInt32(out var mVal))
                                memLimitMb = mVal;
                        }
                        catch { }
                    }
                    var edgeBundle = WasmEdgeMeshEngine.Instance.GenerateEdgeBundle(workerName, memLimitMb);
                    await WriteJsonResponseAsync(res, edgeBundle);
                    break;

                case "/api/dr/regions":
                    var drRegions = DisasterRecoveryEngine.Instance.GetRegions();
                    await WriteJsonResponseAsync(res, new { regions = drRegions });
                    break;

                case "/api/dr/failover":
                    string outageRegion = "eu-central-1";
                    double rpoTarget = 1.0;
                    double rtoTarget = 30.0;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("outageRegionId", out var oProp))
                                outageRegion = oProp.GetString() ?? outageRegion;
                            if (doc.RootElement.TryGetProperty("rpoTargetSec", out var rpoProp) && rpoProp.TryGetDouble(out var rpoVal))
                                rpoTarget = rpoVal;
                            if (doc.RootElement.TryGetProperty("rtoTargetSec", out var rtoProp) && rtoProp.TryGetDouble(out var rtoVal))
                                rtoTarget = rtoVal;
                        }
                        catch { }
                    }
                    var drSim = DisasterRecoveryEngine.Instance.SimulateRegionalBlackout(outageRegion, rpoTarget, rtoTarget);
                    await WriteJsonResponseAsync(res, drSim);
                    break;

                case "/api/dr/runbook":
                    double rbRpo = 1.0;
                    double rbRto = 30.0;
                    if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                        var json = await reader.ReadToEndAsync();
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("targetRpoSeconds", out var rpProp) && rpProp.TryGetDouble(out var rpVal))
                                rbRpo = rpVal;
                            if (doc.RootElement.TryGetProperty("targetRtoSeconds", out var rtProp) && rtProp.TryGetDouble(out var rtVal))
                                rbRto = rtVal;
                        }
                        catch { }
                    }
                    var drRunbook = DisasterRecoveryEngine.Instance.GenerateFailoverRunbook(rbRpo, rbRto);
                    await WriteJsonResponseAsync(res, drRunbook);
                    break;

                default:
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    res.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            res.StatusCode = (int)HttpStatusCode.InternalServerError;
            var errBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            res.ContentType = "application/json; charset=utf-8";
            await res.OutputStream.WriteAsync(errBytes);
            res.Close();
        }
    }

    private static async Task WriteJsonResponseAsync<T>(HttpListenerResponse res, T obj)
    {
        res.ContentType = "application/json; charset=utf-8";
        res.StatusCode = (int)HttpStatusCode.OK;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    public void Dispose()
    {
        Stop();
    }
}
