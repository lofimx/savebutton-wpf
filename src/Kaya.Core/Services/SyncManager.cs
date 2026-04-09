namespace Kaya.Core.Services;

public class SyncManager : IDisposable
{
    private const int SyncIntervalSeconds = 60;

    private readonly SettingsService _settingsService;
    private readonly SyncService _syncService;
    private Timer? _timer;
    private bool _isRunning;

    public SyncManager(SettingsService settingsService, CredentialService credentialService)
    {
        _settingsService = settingsService;
        _syncService = new SyncService(settingsService, credentialService);
    }

    public SyncService SyncService => _syncService;
    public SettingsService SettingsService => _settingsService;

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        RunSync();
        _timer = new Timer(_ => RunSync(), null,
            TimeSpan.FromSeconds(SyncIntervalSeconds),
            TimeSpan.FromSeconds(SyncIntervalSeconds));
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _timer?.Dispose();
        _timer = null;
    }

    public async void TriggerSync()
    {
        await RunSyncAsync();
    }

    private async void RunSync()
    {
        await RunSyncAsync();
    }

    private async Task RunSyncAsync()
    {
        if (!_settingsService.ShouldSync()) return;

        _settingsService.SyncInProgress = true;
        try
        {
            var result = await _syncService.SyncAsync();
            if (result.Errors.Count > 0)
            {
                var errorMsg = string.Join("; ", result.Errors.Select(e => $"{e.Operation} {e.File}: {e.Error}"));
                _settingsService.LastSyncError = errorMsg;
            }
            else
            {
                _settingsService.LastSyncError = "";
                _settingsService.LastSyncSuccess = DateTimeOffset.UtcNow.ToString("o");
            }
        }
        catch (Exception e)
        {
            _settingsService.LastSyncError = e.Message;
        }
        finally
        {
            _settingsService.SyncInProgress = false;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
