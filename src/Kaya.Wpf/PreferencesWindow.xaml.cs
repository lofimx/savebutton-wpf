using System.Windows;
using System.Windows.Media.Imaging;
using Kaya.Core.Models;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public partial class PreferencesWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly AuthService _authService;
    private readonly SyncService _syncService;
    private bool _loaded;

    public PreferencesWindow(SettingsService settingsService, AuthService authService)
    {
        InitializeComponent();
        this.ApplyDarkTitleBar();

        _settingsService = settingsService;
        _authService = authService;
        _syncService = new SyncService(settingsService, authService);

        LoadIcons();
        LoadSettings();
        Render();

        _loaded = true;

        _settingsService.Changed += OnSettingsChanged;
        Closed += (_, _) => _settingsService.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(Render);
    }

    private void LoadIcons()
    {
        GoogleIcon.Source = LoadIcon("icon_google");
        MicrosoftIcon.Source = LoadIcon("icon_microsoft");
        AppleIcon.Source = LoadIcon("icon_apple");
    }

    private static BitmapImage? LoadIcon(string key)
    {
        var uri = new Uri($"pack://application:,,,/Assets/{key}.svg", UriKind.Absolute);
        try
        {
            var resource = Application.GetResourceStream(uri);
            if (resource is null)
            {
                Logger.Instance.Log($"🟠 WARN PreferencesWindow missing icon resource: {uri}");
                return null;
            }
            using var stream = resource.Stream;
            return SvgRenderer.RenderToBitmap(stream, maxWidth: 64);
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR PreferencesWindow LoadIcon({key}): {e.Message}");
            return null;
        }
    }

    private void LoadSettings()
    {
        ServerUrlEntry.Text = _settingsService.ServerUrl;
        NativeHostPortEntry.Text = _settingsService.NativeHostPort.ToString();
        UpdatePrivateUrlWarning(_settingsService.ServerUrl);
    }

    private void Render()
    {
        var signedIn = _settingsService.ShouldSync();
        SignedOutPanel.Visibility = signedIn ? Visibility.Collapsed : Visibility.Visible;
        SignedInPanel.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;

        if (signedIn)
        {
            var info = ProviderInfo.ForIdentityProvider(_settingsService.AuthIdentityProvider);
            ConnectedProviderIcon.Source = LoadIcon(info.IconKey);
            ConnectedProviderText.Text = $"Signed in with {info.Label}";
            ConnectedEmailText.Text = _settingsService.AuthEmail;
            UpdateSyncStatus();
        }
    }

    private void UpdateSyncStatus()
    {
        if (_settingsService.SyncInProgress)
        {
            SyncStatusText.Text = "Syncing\u2026";
            ForceSyncButton.IsEnabled = false;
            return;
        }

        ForceSyncButton.IsEnabled = true;

        var lastError = _settingsService.LastSyncError;
        var lastSuccess = _settingsService.LastSyncSuccess;

        if (!string.IsNullOrEmpty(lastError))
        {
            SyncStatusText.Text = $"Error: {lastError}";
        }
        else if (!string.IsNullOrEmpty(lastSuccess))
        {
            if (DateTimeOffset.TryParse(lastSuccess, out var dt))
                SyncStatusText.Text = $"Last sync: {dt.ToLocalTime():g}";
            else
                SyncStatusText.Text = $"Last sync: {lastSuccess}";
        }
        else
        {
            SyncStatusText.Text = $"Ready to sync with {_settingsService.ServerUrl}";
        }
    }

    private void UpdatePrivateUrlWarning(string url)
    {
        PrivateUrlWarning.Visibility = ServerUrlHelper.IsPrivateHost(url)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnServerUrlChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdatePrivateUrlWarning(ServerUrlEntry.Text);
    }

    private void OnSaveServerUrl(object sender, RoutedEventArgs e)
    {
        var url = ServerUrlEntry.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        _settingsService.ServerUrl = url;
        Logger.Instance.Log($"🔵 INFO PreferencesWindow server URL saved: {url}");
    }

    private async void OnSignInEmail(object sender, RoutedEventArgs e)
    {
        var email = EmailEntry.Text.Trim();
        var password = PasswordEntry.Password;
        var server = ServerUrlEntry.Text.Trim();

        if (string.IsNullOrEmpty(server))
        {
            SignInStatusText.Text = "Enter a server URL";
            return;
        }
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SignInStatusText.Text = "Enter both email and password";
            return;
        }

        _settingsService.ServerUrl = server;

        SignInStatusText.Text = "Signing in\u2026";
        SignInButton.IsEnabled = false;
        try
        {
            var result = await _authService.ExchangePasswordAsync(email, password);
            if (!result.Success)
            {
                SignInStatusText.Text = result.Error ?? "Sign-in failed.";
                return;
            }
            PasswordEntry.Password = "";
            EmailEntry.Text = "";
            SignInStatusText.Text = "";
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private void OnSignInGoogle(object sender, RoutedEventArgs e) =>
        StartBrowserFlow("google_oauth2");

    private void OnSignInMicrosoft(object sender, RoutedEventArgs e) =>
        StartBrowserFlow("microsoft_graph");

    private void OnSignUp(object sender, RoutedEventArgs e) =>
        StartBrowserFlow("register");

    private void StartBrowserFlow(string providerPath)
    {
        var server = ServerUrlEntry.Text.Trim();
        if (string.IsNullOrEmpty(server))
        {
            SignInStatusText.Text = "Enter a server URL";
            return;
        }
        _settingsService.ServerUrl = server;

        if (ServerUrlHelper.IsPrivateHost(server))
        {
            SignInStatusText.Text = "OAuth won't work with a LAN / localhost URL. Use email/password or an ngrok tunnel.";
            return;
        }

        SignInStatusText.Text = "Opening browser\u2026 complete sign-in there, then return here.";
        _authService.StartBrowserLogin(providerPath);
    }

    private async void OnSignOut(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Sign out of Save Button on this device?",
            "Sign Out?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await _authService.SignOutAsync();
    }

    private async void OnForceSync(object sender, RoutedEventArgs e)
    {
        if (!_settingsService.ShouldSync()) return;

        ForceSyncButton.IsEnabled = false;
        SyncStatusText.Text = "Syncing\u2026";
        _settingsService.SyncInProgress = true;

        try
        {
            var syncResult = await _syncService.SyncAsync();

            if (syncResult.Errors.Count > 0)
            {
                var errorMsg = string.Join("\n",
                    syncResult.Errors.Select(err => $"{err.Operation} {err.File}: {err.Error}"));
                _settingsService.LastSyncError = errorMsg;
            }
            else
            {
                _settingsService.LastSyncError = "";
                _settingsService.LastSyncSuccess = DateTimeOffset.UtcNow.ToString("o");
            }

            UpdateSyncStatus();
        }
        catch (Exception ex)
        {
            _settingsService.LastSyncError = ex.Message;
            UpdateSyncStatus();
        }
        finally
        {
            _settingsService.SyncInProgress = false;
            ForceSyncButton.IsEnabled = true;
        }
    }

    private void OnSaveNativeHostPort(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(NativeHostPortEntry.Text.Trim(), out var port) && port is > 0 and <= 65535)
        {
            _settingsService.NativeHostPort = port;
            Logger.Instance.Log($"🔵 INFO PreferencesWindow native host port changed to {port}");
        }
        else
        {
            NativeHostPortEntry.Text = _settingsService.NativeHostPort.ToString();
        }
    }
}
