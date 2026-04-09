using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaya.Core.Services;

public record ShareResult(bool Ok, string ShareUrl = "");

public class ShareService
{
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly HttpClient _httpClient;

    public ShareService(SettingsService settingsService, CredentialService credentialService)
    {
        _settingsService = settingsService;
        _credentialService = credentialService;
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
        var baseUrl = _settingsService.ServerUrl;
        var email = _settingsService.Email;
        var password = _credentialService.GetPassword();

        if (string.IsNullOrEmpty(password))
        {
            Logger.Instance.Log("🟠 WARN ShareService no password configured");
            return new ShareResult(false);
        }

        var url = BuildShareUrl(baseUrl, email, filename);
        Logger.Instance.Log($"🔵 INFO ShareService sharing \"{filename}\" via {baseUrl}");

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

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
