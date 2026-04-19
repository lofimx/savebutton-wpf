using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaya.Core.Services;

public record ShareResult(bool Ok, string ShareUrl = "");

public class ShareService
{
    private readonly SettingsService _settingsService;
    private readonly AuthService _authService;
    private readonly HttpClient _httpClient;

    public ShareService(SettingsService settingsService, AuthService authService)
    {
        _settingsService = settingsService;
        _authService = authService;
        _httpClient = new HttpClient();
    }

    public static string BuildShareUrl(string baseUrl, string email, string filename)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedFilename = Uri.EscapeDataString(filename);
        return $"{baseUrl}/api/v1/{encodedEmail}/share/anga/{encodedFilename}";
    }

    public async Task<ShareResult> ShareAsync(string filename)
    {
        var baseUrl = _settingsService.NormalizedServerUrl;
        var email = _settingsService.AuthEmail;

        var authHeader = await _authService.GetAuthHeaderAsync();
        if (authHeader is null)
        {
            Logger.Instance.Log("🟠 WARN ShareService no auth token available");
            return new ShareResult(false);
        }

        var url = BuildShareUrl(baseUrl, email, filename);
        Logger.Instance.Log($"🔵 INFO ShareService sharing \"{filename}\" via {baseUrl}");

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = authHeader;

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Instance.Log(
                    $"🟠 WARN ShareService share failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return new ShareResult(false);
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<ShareResponse>(responseText);
            var shareUrl = json?.ShareUrl ?? "";

            Logger.Instance.Log($"🔵 INFO ShareService share succeeded: {shareUrl}");
            return new ShareResult(true, shareUrl);
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR ShareService share error: {e.Message}");
            return new ShareResult(false);
        }
    }

    private class ShareResponse
    {
        [JsonPropertyName("share_url")]
        public string ShareUrl { get; set; } = "";
    }
}
