using System.Net;
using System.Text;
using System.Text.Json;
using Kaya.Core.Models;

namespace Kaya.Core.Services;

public class NativeHostServer
{
    private static readonly string KayaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya");

    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public Action<string, string>? OnFileReceived { get; set; }

    public NativeHostServer(SettingsService settingsService, CredentialService credentialService)
    {
        _settingsService = settingsService;
        _credentialService = credentialService;
    }

    public void Start()
    {
        var port = _settingsService.NativeHostPort;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        try
        {
            _listener.Start();
            Logger.Instance.Log($"🔵 INFO NativeHostServer listening on 127.0.0.1:{port}");
            _ = ListenLoopAsync(_cts.Token);
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR NativeHostServer failed to start on port {port}: {e.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        Logger.Instance.Log("🔵 INFO NativeHostServer stopped");
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Logger.Instance.Error($"🔴 ERROR NativeHostServer listener: {e.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        var method = request.HttpMethod;
        var path = request.Url?.AbsolutePath ?? "/";
        var parsed = new NativeHostRequest(method, path);

        try
        {
            switch (parsed.Type)
            {
                case NativeHostRequestType.Preflight:
                    response.StatusCode = 204;
                    break;
                case NativeHostRequestType.Health:
                    await WriteResponse(response, 200, "text/plain", "ok");
                    break;
                case NativeHostRequestType.Config:
                    await HandleConfig(request, response);
                    break;
                case NativeHostRequestType.Listing listing:
                    await HandleListing(response, listing.Collection);
                    break;
                case NativeHostRequestType.FileWrite fw:
                    await HandleFileWrite(request, response, fw.Collection, fw.Filename);
                    break;
                case NativeHostRequestType.Invalid invalid:
                    await WriteResponse(response, 400, "text/plain", invalid.Reason);
                    Logger.Instance.Log($"🟠 WARN NativeHostServer invalid request: {invalid.Reason}");
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR NativeHostServer handling {method} {path}: {e.Message}");
            await WriteResponse(response, 500, "text/plain", "Internal server error");
        }
        finally
        {
            response.Close();
        }
    }

    private async Task HandleListing(HttpListenerResponse response, string collection)
    {
        var dir = Path.Combine(KayaDir, collection);
        string[] items;

        if (collection == "words")
        {
            items = Directory.Exists(dir)
                ? Directory.GetDirectories(dir).Select(Path.GetFileName).Where(f => f != null && !f.StartsWith('.')).Cast<string>().ToArray()
                : [];
        }
        else
        {
            items = Directory.Exists(dir)
                ? Directory.GetFiles(dir).Select(Path.GetFileName).Where(f => f != null && !f.StartsWith('.')).Cast<string>().ToArray()
                : [];
        }

        var body = string.Join("\n", items) + "\n";
        await WriteResponse(response, 200, "text/plain", body);
        Logger.Instance.Log($"🟢 DEBUG NativeHostServer GET /{collection}: {items.Length} items");
    }

    private async Task HandleFileWrite(HttpListenerRequest request, HttpListenerResponse response,
        string collection, string filename)
    {
        var dir = Path.Combine(KayaDir, collection);
        Directory.CreateDirectory(dir);

        using var ms = new MemoryStream();
        await request.InputStream.CopyToAsync(ms);
        var data = ms.ToArray();

        if (data.Length == 0)
        {
            await WriteResponse(response, 400, "text/plain", "Empty request body");
            return;
        }

        var filePath = Path.Combine(dir, filename);
        await File.WriteAllBytesAsync(filePath, data);

        await WriteResponse(response, 200, "text/plain", "ok");
        Logger.Instance.Log($"🔵 INFO NativeHostServer wrote {collection}/{filename}");

        OnFileReceived?.Invoke(collection, filename);
    }

    private async Task HandleConfig(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var json = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(json))
        {
            await WriteResponse(response, 400, "application/json", "{\"error\":\"Empty request body\"}");
            return;
        }

        var config = JsonSerializer.Deserialize<ConfigPayload>(json);
        if (config is null)
        {
            await WriteResponse(response, 400, "application/json", "{\"error\":\"Invalid JSON\"}");
            return;
        }

        if (!string.IsNullOrEmpty(config.Server))
            _settingsService.ServerUrl = config.Server;
        if (!string.IsNullOrEmpty(config.Email))
            _settingsService.Email = config.Email;
        if (!string.IsNullOrEmpty(config.Password))
            _credentialService.SetPassword(config.Password);

        _settingsService.SyncEnabled = true;

        await WriteResponse(response, 200, "application/json", "{\"ok\":true}");
        Logger.Instance.Log("🔵 INFO NativeHostServer config updated via POST /config");
    }

    private static async Task WriteResponse(HttpListenerResponse response, int statusCode, string contentType, string body)
    {
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private class ConfigPayload
    {
        public string? Server { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}
