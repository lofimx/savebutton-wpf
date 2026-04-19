using System.Windows;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public partial class App : Application
{
    private const string CallbackScheme = "savebutton://";

    private SyncManager? _syncManager;
    private NativeHostServer? _nativeHostServer;
    private SingleInstance? _singleInstance;
    private AuthService? _authService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Explicit flag used by packaging / scripts: register the scheme and exit immediately.
        if (HasArg(e.Args, "--register-url-scheme"))
        {
            UrlSchemeRegistrar.EnsureRegistered();
            Shutdown(0);
            return;
        }

        _singleInstance = new SingleInstance();
        var isPrimary = _singleInstance.TryAcquirePrimary();

        var incomingUri = ExtractCallbackUri(e.Args);
        Logger.Instance.Log($"🔵 INFO App OnStartup primary={isPrimary} hasCallbackUri={!string.IsNullOrEmpty(incomingUri)}");

        if (!isPrimary)
        {
            if (!string.IsNullOrEmpty(incomingUri))
            {
                Logger.Instance.Log("🔵 INFO App forwarding callback URI to primary instance");
                _singleInstance.ForwardToPrimary(incomingUri);
            }
            else
            {
                Logger.Instance.Log("🟠 WARN App second instance with no URI; exiting");
            }
            Shutdown(0);
            return;
        }

        UrlSchemeRegistrar.EnsureRegistered();

        var settingsService = new SettingsService();
        var credentialService = new CredentialService();
        credentialService.RemoveLegacyCredentials();
        _authService = new AuthService(settingsService, credentialService);
        _syncManager = new SyncManager(settingsService, _authService);
        _syncManager.Start();

        _nativeHostServer = new NativeHostServer(settingsService, credentialService);
        _nativeHostServer.Start();

        _singleInstance.OnUrlReceived = HandleCallbackUri;
        _singleInstance.StartPipeServer();

        _mainWindow = new MainWindow
        {
            SyncManager = _syncManager
        };

        _nativeHostServer.OnFileReceived = (collection, filename) =>
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                _syncManager.TriggerSync();
            });
        };

        _mainWindow.Show();

        if (!string.IsNullOrEmpty(incomingUri))
            HandleCallbackUri(incomingUri);
    }

    private void HandleCallbackUri(string uri)
    {
        Dispatcher.Invoke(async () =>
        {
            try
            {
                var parsed = new Uri(uri);
                var query = ParseQuery(parsed.Query);
                query.TryGetValue("code", out var code);
                query.TryGetValue("error", out var error);

                if (!string.IsNullOrEmpty(error))
                {
                    Logger.Instance.Log($"🟠 WARN App OAuth callback error: {error}");
                    BringWindowForward();
                    MessageBox.Show(_mainWindow,
                        $"Sign-in was cancelled or failed: {error}",
                        "Sign-in failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(code))
                {
                    Logger.Instance.Log("🟠 WARN App OAuth callback missing code");
                    return;
                }

                if (_authService is null) return;

                var result = await _authService.ExchangeAuthorizationCodeAsync(code);
                BringWindowForward();

                if (!result.Success)
                {
                    MessageBox.Show(_mainWindow,
                        result.Error ?? "Sign-in failed.",
                        "Sign-in failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _syncManager?.TriggerSync();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"🔴 ERROR App callback URI handler: {ex.Message}");
            }
        });
    }

    private void BringWindowForward()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = "";
            }
            else
            {
                var key = Uri.UnescapeDataString(pair[..eq]);
                var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
                result[key] = value;
            }
        }
        return result;
    }

    private static string? ExtractCallbackUri(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.StartsWith(CallbackScheme, StringComparison.OrdinalIgnoreCase))
                return arg;
        }
        return null;
    }

    private static bool HasArg(string[] args, string flag) =>
        args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    protected override void OnExit(ExitEventArgs e)
    {
        _nativeHostServer?.Stop();
        _syncManager?.Stop();
        _syncManager?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
