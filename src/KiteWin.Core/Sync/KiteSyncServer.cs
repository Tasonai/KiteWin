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

        try
        {
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
                        var responseObj = new { status = success ? "ok" : "error" };
                        await WriteJsonResponseAsync(res, responseObj);
                    }
                    else
                    {
                        res.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        res.Close();
                    }
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
