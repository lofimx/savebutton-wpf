using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaya.Core.Services;

public class AuthResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? UserEmail { get; init; }
    public string? IdentityProvider { get; init; }
}

public class AuthService
{
    private const string DeviceType = "desktop_windows";
    private const string ExtensionCallbackPath = "/oauth/extension-callback";

    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly HttpClient _httpClient;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthService(SettingsService settingsService, CredentialService credentialService)
    {
        _settingsService = settingsService;
        _credentialService = credentialService;
        _httpClient = new HttpClient();
    }

    public AuthService(SettingsService settingsService, CredentialService credentialService, HttpClient httpClient)
    {
        _settingsService = settingsService;
        _credentialService = credentialService;
        _httpClient = httpClient;
    }

    public bool IsSignedIn => AuthState.IsSignedIn(_settingsService.AuthMethod, _settingsService.AuthEmail);

    public async Task<AuthenticationHeaderValue?> GetAuthHeaderAsync()
    {
        var token = await GetAccessTokenAsync();
        return token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _accessTokenExpiry - TimeSpan.FromSeconds(30))
            return _accessToken;

        if (!IsSignedIn) return null;

        return await RefreshAccessTokenAsync();
    }

    public async Task<string?> RefreshAccessTokenAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var refreshToken = _credentialService.GetRefreshToken();
            if (string.IsNullOrEmpty(refreshToken))
            {
                Logger.Instance.Log("🟠 WARN AuthService refresh requested with no stored refresh token");
                return null;
            }

            var deviceName = Environment.MachineName;
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["device_name"] = deviceName,
                ["device_type"] = DeviceType,
            });

            var url = $"{_settingsService.NormalizedServerUrl}/api/v1/auth/token";
            try
            {
                var response = await _httpClient.PostAsync(url, form);
                var body = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode is not (200 or 201))
                {
                    Logger.Instance.Log($"🟠 WARN AuthService refresh failed: {(int)response.StatusCode} {body}");
                    return null;
                }

                var payload = JsonSerializer.Deserialize<TokenResponse>(body);
                if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
                {
                    Logger.Instance.Log("🟠 WARN AuthService refresh returned malformed token");
                    return null;
                }

                ApplyTokenResponse(payload);
                return _accessToken;
            }
            catch (Exception e)
            {
                Logger.Instance.Error($"🔴 ERROR AuthService refresh exception: {e.Message}");
                return null;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<AuthResult> ExchangePasswordAsync(string email, string password)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["email"] = email,
            ["password"] = password,
            ["device_name"] = Environment.MachineName,
            ["device_type"] = DeviceType,
        });

        return await PostTokenExchange(form, identityProviderFallback: "");
    }

    public async Task<AuthResult> ExchangeAuthorizationCodeAsync(string code)
    {
        var verifier = _settingsService.AuthPkceVerifier;
        if (string.IsNullOrEmpty(verifier))
        {
            return new AuthResult { Success = false, Error = "Missing PKCE verifier; please retry sign-in." };
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = BuildRedirectUri(),
            ["device_name"] = Environment.MachineName,
            ["device_type"] = DeviceType,
        });

        var result = await PostTokenExchange(form, identityProviderFallback: "");
        // Clear verifier once the exchange resolves, success or failure — it is single-use.
        _settingsService.AuthPkceVerifier = "";
        return result;
    }

    private async Task<AuthResult> PostTokenExchange(FormUrlEncodedContent form, string identityProviderFallback)
    {
        var url = $"{_settingsService.NormalizedServerUrl}/api/v1/auth/token";
        try
        {
            var response = await _httpClient.PostAsync(url, form);
            var body = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode is not (200 or 201))
            {
                Logger.Instance.Log($"🟠 WARN AuthService token exchange failed: {(int)response.StatusCode} {body}");
                return new AuthResult { Success = false, Error = $"Sign-in failed ({(int)response.StatusCode})." };
            }

            var payload = JsonSerializer.Deserialize<TokenResponse>(body);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken) || string.IsNullOrEmpty(payload.RefreshToken))
            {
                Logger.Instance.Log("🟠 WARN AuthService token exchange returned malformed body");
                return new AuthResult { Success = false, Error = "Server returned malformed token response." };
            }

            var email = !string.IsNullOrEmpty(payload.UserEmail)
                ? payload.UserEmail
                : payload.Email ?? "";
            var identityProvider = payload.IdentityProvider ?? identityProviderFallback;

            ApplyTokenResponse(payload);

            _credentialService.SetRefreshToken(payload.RefreshToken);
            _settingsService.SetAuthState(AuthState.TokenMethod, email, identityProvider);

            Logger.Instance.Log($"🔵 INFO AuthService signed in as {email} (provider={identityProvider})");
            return new AuthResult { Success = true, UserEmail = email, IdentityProvider = identityProvider };
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR AuthService token exchange exception: {e.Message}");
            return new AuthResult { Success = false, Error = e.Message };
        }
    }

    private void ApplyTokenResponse(TokenResponse payload)
    {
        _accessToken = payload.AccessToken;
        var lifetime = payload.ExpiresIn > 0 ? payload.ExpiresIn : 900;
        _accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(lifetime);
    }

    public async Task SignOutAsync()
    {
        var refreshToken = _credentialService.GetRefreshToken();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["refresh_token"] = refreshToken,
            });
            var url = $"{_settingsService.NormalizedServerUrl}/api/v1/auth/revoke";
            try
            {
                var response = await _httpClient.PostAsync(url, form);
                Logger.Instance.Log($"🔵 INFO AuthService revoke returned {(int)response.StatusCode}");
            }
            catch (Exception e)
            {
                Logger.Instance.Log($"🟠 WARN AuthService revoke failed (continuing sign-out): {e.Message}");
            }
        }

        ClearLocalAuth();
    }

    public void ClearLocalAuth()
    {
        _accessToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        _credentialService.ClearRefreshToken();
        _settingsService.ClearAuthState();
        Logger.Instance.Log("🔵 INFO AuthService local auth state cleared");
    }

    public void StartBrowserLogin(string providerPath)
    {
        var (verifier, challenge) = Pkce.Generate();
        _settingsService.AuthPkceVerifier = verifier;

        var redirectUri = BuildRedirectUri();
        var server = _settingsService.NormalizedServerUrl;
        var query = $"code_challenge={Uri.EscapeDataString(challenge)}" +
                    $"&code_challenge_method=S256" +
                    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                    $"&device_name={Uri.EscapeDataString(Environment.MachineName)}" +
                    $"&device_type={DeviceType}";
        var url = $"{server}/api/v1/auth/authorize/{providerPath}?{query}";

        Logger.Instance.Log($"🔵 INFO AuthService launching browser for provider={providerPath}");
        OpenUrlInBrowser(url);
    }

    public string BuildRedirectUri() =>
        $"{_settingsService.NormalizedServerUrl}{ExtensionCallbackPath}";

    private static void OpenUrlInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR AuthService failed to open browser: {e.Message}");
        }
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("user_email")] public string? UserEmail { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("identity_provider")] public string? IdentityProvider { get; set; }
    }
}
