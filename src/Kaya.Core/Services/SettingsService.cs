using System.Text.Json;

namespace Kaya.Core.Services;

public class SettingsService
{
    private const string DefaultServerUrl = "https://savebutton.com";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya", "settings.json");

    private SettingsData _data;

    public event Action? Changed;

    public SettingsService()
    {
        _data = Load();
    }

    public string ServerUrl
    {
        get => string.IsNullOrEmpty(_data.ServerUrl) ? DefaultServerUrl : _data.ServerUrl;
        set { _data.ServerUrl = value; Save(); Changed?.Invoke(); }
    }

    public string NormalizedServerUrl => ServerUrlHelper.Normalize(ServerUrl);

    public string Email
    {
        get => _data.Email ?? "";
        set { _data.Email = value; Save(); Changed?.Invoke(); }
    }

    /// <summary>Retained for legacy settings files only. Do not use as a "signed in" signal.</summary>
    public bool SyncEnabled
    {
        get => _data.SyncEnabled;
        set { _data.SyncEnabled = value; Save(); Changed?.Invoke(); }
    }

    public string AuthMethod
    {
        get => _data.AuthMethod ?? "";
        set { _data.AuthMethod = value; Save(); Changed?.Invoke(); }
    }

    public string AuthEmail
    {
        get => _data.AuthEmail ?? "";
        set { _data.AuthEmail = value; Save(); Changed?.Invoke(); }
    }

    public string AuthIdentityProvider
    {
        get => _data.AuthIdentityProvider ?? "";
        set { _data.AuthIdentityProvider = value; Save(); Changed?.Invoke(); }
    }

    public string AuthPkceVerifier
    {
        get => _data.AuthPkceVerifier ?? "";
        set { _data.AuthPkceVerifier = value; Save(); Changed?.Invoke(); }
    }

    public string LastSyncError
    {
        get => _data.LastSyncError ?? "";
        set { _data.LastSyncError = value; Save(); Changed?.Invoke(); }
    }

    public string LastSyncSuccess
    {
        get => _data.LastSyncSuccess ?? "";
        set { _data.LastSyncSuccess = value; Save(); Changed?.Invoke(); }
    }

    public const int DefaultNativeHostPort = 21420;

    public int NativeHostPort
    {
        get => _data.NativeHostPort ?? DefaultNativeHostPort;
        set { _data.NativeHostPort = value; Save(); Changed?.Invoke(); }
    }

    private bool _syncInProgress;

    public bool SyncInProgress
    {
        get => _syncInProgress;
        set { _syncInProgress = value; Changed?.Invoke(); }
    }

    public bool ShouldSync()
    {
        return AuthState.IsSignedIn(AuthMethod, AuthEmail);
    }

    public bool IsCustomServerConfigured()
    {
        return ServerUrl != DefaultServerUrl && ServerUrl.Length > 0;
    }

    /// <summary>
    /// Atomically set the signed-in auth state. All four fields are written in one Save().
    /// </summary>
    public void SetAuthState(string method, string email, string identityProvider)
    {
        _data.AuthMethod = method;
        _data.AuthEmail = email;
        _data.AuthIdentityProvider = identityProvider;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Atomically clear all signed-in auth state. Does not touch refresh token.</summary>
    public void ClearAuthState()
    {
        _data.AuthMethod = "";
        _data.AuthEmail = "";
        _data.AuthIdentityProvider = "";
        _data.AuthPkceVerifier = "";
        _data.SyncEnabled = false;
        Save();
        Changed?.Invoke();
    }

    private SettingsData Load()
    {
        if (!File.Exists(SettingsPath))
            return new SettingsData();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private class SettingsData
    {
        public string? ServerUrl { get; set; }
        public string? Email { get; set; }
        public bool SyncEnabled { get; set; }
        public string? LastSyncError { get; set; }
        public string? LastSyncSuccess { get; set; }
        public int? NativeHostPort { get; set; }
        public string? AuthMethod { get; set; }
        public string? AuthEmail { get; set; }
        public string? AuthIdentityProvider { get; set; }
        public string? AuthPkceVerifier { get; set; }
    }
}
