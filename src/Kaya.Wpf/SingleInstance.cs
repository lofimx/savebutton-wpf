using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using Kaya.Core.Services;

namespace Kaya.Wpf;

/// <summary>
/// Enforces a single WPF instance per-user and delivers savebutton:// callback URIs
/// arriving at freshly spawned processes to the already-running primary instance over
/// a named pipe.
/// </summary>
public class SingleInstance : IDisposable
{
    private const string MutexBase = "Local\\Kaya.Wpf.SingleInstance";
    private const string PipeBase = "Kaya.Wpf";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public Action<string>? OnUrlReceived { get; set; }

    public SingleInstance()
    {
        var sid = CurrentUserSid();
        _mutexName = $"{MutexBase}.{sid}";
        _pipeName = $"{PipeBase}.{sid}";
    }

    public bool TryAcquirePrimary()
    {
        _mutex = new Mutex(initiallyOwned: true, name: _mutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }
        return createdNew;
    }

    public void StartPipeServer()
    {
        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunPipeLoop(_cts.Token));
    }

    public void ForwardToPrimary(string uri)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(2000);
            var bytes = Encoding.UTF8.GetBytes(uri);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR SingleInstance forward failed: {e.Message}");
        }
    }

    private async Task RunPipeLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var uri = (await reader.ReadToEndAsync(ct)).Trim();
                if (!string.IsNullOrEmpty(uri))
                {
                    Logger.Instance.Log($"🔵 INFO SingleInstance received forwarded URI (len={uri.Length})");
                    OnUrlReceived?.Invoke(uri);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Logger.Instance.Error($"🔴 ERROR SingleInstance pipe loop: {e.Message}");
                await Task.Delay(500, ct);
            }
        }
    }

    private static string CurrentUserSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? "default";
        }
        catch
        {
            return "default";
        }
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        GC.SuppressFinalize(this);
    }
}
