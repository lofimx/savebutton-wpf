using System.Windows;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public partial class App : Application
{
    private SyncManager? _syncManager;
    private NativeHostServer? _nativeHostServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var credentialService = new CredentialService();
        _syncManager = new SyncManager(settingsService, credentialService);
        _syncManager.Start();

        _nativeHostServer = new NativeHostServer(settingsService, credentialService);
        _nativeHostServer.Start();

        var mainWindow = new MainWindow
        {
            SyncManager = _syncManager
        };

        // When the native host receives a file, refresh the UI
        _nativeHostServer.OnFileReceived = (collection, filename) =>
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                _syncManager.TriggerSync();
            });
        };

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _nativeHostServer?.Stop();
        _syncManager?.Stop();
        _syncManager?.Dispose();
        base.OnExit(e);
    }
}
