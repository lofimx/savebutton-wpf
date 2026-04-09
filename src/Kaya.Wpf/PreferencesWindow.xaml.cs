using System.Windows;
using System.Windows.Threading;
using Kaya.Core.Services;

namespace Kaya.Wpf;

public partial class PreferencesWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly SyncService _syncService;

    public PreferencesWindow(SettingsService settingsService, CredentialService credentialService)
    {
        InitializeComponent();
        this.ApplyDarkTitleBar();

        _settingsService = settingsService;
        _credentialService = credentialService;
        _syncService = new SyncService(settingsService, credentialService);

        LoadSettings();

        _settingsService.Changed += OnSettingsChanged;
        Closed += (_, _) => _settingsService.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(UpdateStatus);
    }

    private void LoadSettings()
    {
        ServerUrlEntry.Text = _settingsService.ServerUrl;
        EmailEntry.Text = _settingsService.Email;
        NativeHostPortEntry.Text = _settingsService.NativeHostPort.ToString();

        var password = _credentialService.GetPassword();
        if (!string.IsNullOrEmpty(password))
            PasswordEntry.Password = password;

        UpdateStatus();
    }

    private void OnSaveCredentials(object sender, RoutedEventArgs e)
    {
        var serverUrl = ServerUrlEntry.Text.Trim();
        var email = EmailEntry.Text.Trim();
        var password = PasswordEntry.Password;

        if (string.IsNullOrEmpty(serverUrl))
        {
            SyncStatusText.Text = "Enter a server URL";
            return;
        }

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            SyncStatusText.Text = "Enter both email and password";
            return;
        }

        _settingsService.ServerUrl = serverUrl;
        _settingsService.Email = email;
        _credentialService.SetPassword(password);
        _settingsService.SyncEnabled = true;

        UpdateStatus();
        Logger.Instance.Log("🔵 INFO PreferencesWindow credentials saved");
    }

    private void OnClearCredentials(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear your email and password?",
            "Clear Credentials?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        _settingsService.SyncEnabled = false;
        _settingsService.Email = "";
        _credentialService.ClearPassword();

        EmailEntry.Text = "";
        PasswordEntry.Password = "";

        UpdateStatus();
        Logger.Instance.Log("🔵 INFO PreferencesWindow credentials cleared");
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

            UpdateStatus();
        }
        catch (Exception ex)
        {
            _settingsService.LastSyncError = ex.Message;
            UpdateStatus();
        }
        finally
        {
            _settingsService.SyncInProgress = false;
            ForceSyncButton.IsEnabled = true;
        }
    }

    private void UpdateStatus()
    {
        if (_settingsService.SyncInProgress)
        {
            SyncStatusText.Text = "Syncing\u2026";
            ForceSyncButton.IsEnabled = false;
            return;
        }

        ForceSyncButton.IsEnabled = true;

        if (!_settingsService.ShouldSync())
        {
            SyncStatusText.Text = "Not configured";
            return;
        }

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
