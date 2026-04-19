using System.Net;
using System.Net.Http.Headers;
using Kaya.Core.Models;

namespace Kaya.Core.Services;

public class SyncResult
{
    public List<string> Downloaded { get; } = [];
    public List<string> Uploaded { get; } = [];
    public List<SyncError> Errors { get; } = [];
}

public record SyncError(string File, string Operation, string Error);

public class SyncService
{
    private static readonly string LocalAngaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya", "anga");
    private static readonly string LocalMetaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya", "meta");
    private static readonly string LocalWordsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya", "words");

    private readonly SettingsService _settingsService;
    private readonly AuthService _authService;
    private readonly HttpClient _httpClient;
    private bool _isSyncing;

    public bool IsSyncing => _isSyncing;

    public SyncService(SettingsService settingsService, AuthService authService)
    {
        _settingsService = settingsService;
        _authService = authService;
        _httpClient = new HttpClient();
    }

    public async Task<SyncResult> SyncAsync()
    {
        if (_isSyncing)
            return new SyncResult();

        if (!_settingsService.ShouldSync())
            return new SyncResult();

        var authHeader = await _authService.GetAuthHeaderAsync();
        if (authHeader is null)
        {
            Logger.Instance.Log("🟠 WARN SyncService no auth header; clearing sign-in state");
            _authService.ClearLocalAuth();
            return new SyncResult();
        }

        _isSyncing = true;
        var result = new SyncResult();

        try
        {
            var baseUrl = _settingsService.NormalizedServerUrl;
            var email = _settingsService.AuthEmail;

            Directory.CreateDirectory(LocalAngaDir);
            Directory.CreateDirectory(LocalMetaDir);

            await SyncDirectory(baseUrl, email, "anga", LocalAngaDir, result);
            await SyncDirectory(baseUrl, email, "meta", LocalMetaDir, result, "*.toml");
            await SyncWords(baseUrl, email, result);
        }
        finally
        {
            _isSyncing = false;
        }

        return result;
    }

    private async Task SyncDirectory(string baseUrl, string email, string category,
        string localDir, SyncResult result, string? filter = null)
    {
        Directory.CreateDirectory(localDir);

        var serverFiles = await FetchFileList(baseUrl, email, category);
        var localFiles = FetchLocalFiles(localDir, filter);

        var toDownload = serverFiles.Except(localFiles).ToList();
        var toUpload = localFiles.Except(serverFiles).ToList();

        foreach (var filename in toDownload)
        {
            try
            {
                await DownloadFile(baseUrl, email, category, filename, localDir);
                result.Downloaded.Add(filename);
            }
            catch (Exception e)
            {
                result.Errors.Add(new SyncError(filename, "download", e.Message));
            }
        }

        foreach (var filename in toUpload)
        {
            try
            {
                if (!new Filename(filename).IsValid())
                {
                    result.Errors.Add(new SyncError(filename, "upload", "Filename contains URL-illegal characters"));
                    continue;
                }
                await UploadFile(baseUrl, email, category, filename, localDir);
                result.Uploaded.Add(filename);
            }
            catch (Exception e)
            {
                result.Errors.Add(new SyncError(filename, "upload", e.Message));
            }
        }
    }

    private async Task SyncWords(string baseUrl, string email, SyncResult result)
    {
        Directory.CreateDirectory(LocalWordsDir);

        var serverWords = await FetchFileList(baseUrl, email, "words");
        var localWords = FetchLocalDirectories(LocalWordsDir);

        Logger.Instance.Log($"🔵 INFO Words - Server: {serverWords.Count}, Local: {localWords.Count}");

        if (serverWords.Count == localWords.Count)
        {
            Logger.Instance.Log("🟢 DEBUG Words counts match, skipping words sync");
            return;
        }

        var wordsToDownload = serverWords.Except(localWords).ToList();
        Logger.Instance.Log($"🔵 INFO Words - Download: {wordsToDownload.Count}");

        foreach (var word in wordsToDownload)
        {
            await DownloadWord(baseUrl, email, word, result);
        }
    }

    private async Task DownloadWord(string baseUrl, string email, string word, SyncResult result)
    {
        var wordDir = Path.Combine(LocalWordsDir, word);
        Directory.CreateDirectory(wordDir);

        var serverFiles = await FetchFileList(baseUrl, email, $"words/{Uri.EscapeDataString(word)}");
        foreach (var filename in serverFiles)
        {
            try
            {
                await DownloadFile(baseUrl, email, $"words/{Uri.EscapeDataString(word)}", filename, wordDir);
                result.Downloaded.Add($"{word}/{filename}");
            }
            catch (Exception e)
            {
                result.Errors.Add(new SyncError($"{word}/{filename}", "download", e.Message));
            }
        }
    }

    private async Task<List<string>> FetchFileList(string baseUrl, string email, string category)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var url = $"{baseUrl}/api/v1/{encodedEmail}/{category}";

        try
        {
            using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url));
            if (response is null || !response.IsSuccessStatusCode)
                return [];

            var content = await response.Content.ReadAsStringAsync();
            return content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task DownloadFile(string baseUrl, string email, string category,
        string filename, string localDir)
    {
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedFilename = Uri.EscapeDataString(filename);
        var url = $"{baseUrl}/api/v1/{encodedEmail}/{category}/{encodedFilename}";

        using var response = await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url))
            ?? throw new HttpRequestException($"No response for {url}");
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync();
        var localPath = Path.Combine(localDir, filename);
        await File.WriteAllBytesAsync(localPath, data);
    }

    private async Task UploadFile(string baseUrl, string email, string category,
        string filename, string localDir)
    {
        var localPath = Path.Combine(localDir, filename);
        var fileBytes = await File.ReadAllBytesAsync(localPath);

        var encodedEmail = Uri.EscapeDataString(email);
        var encodedFilename = Uri.EscapeDataString(filename);
        var url = $"{baseUrl}/api/v1/{encodedEmail}/{category}/{encodedFilename}";

        using var response = await SendWithRetryAsync(() =>
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypeFor(filename));
            content.Add(fileContent, "file", filename);
            return new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        }) ?? throw new HttpRequestException($"No response for {url}");

        var statusCode = (int)response.StatusCode;
        if (statusCode != 200 && statusCode != 201 && statusCode != 409 && statusCode != 422)
            response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Send an HTTP request with a Bearer header from AuthService. On 401, refresh the token
    /// once and retry. If the second attempt also 401s, clear local auth state and return null.
    /// </summary>
    private async Task<HttpResponseMessage?> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory)
    {
        var header = await _authService.GetAuthHeaderAsync();
        if (header is null) return null;

        var request = requestFactory();
        request.Headers.Authorization = header;
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        Logger.Instance.Log("🟠 WARN SyncService got 401; refreshing token");

        var refreshed = await _authService.RefreshAccessTokenAsync();
        if (string.IsNullOrEmpty(refreshed))
        {
            Logger.Instance.Log("🟠 WARN SyncService refresh failed; clearing auth state");
            _authService.ClearLocalAuth();
            return null;
        }

        var retry = requestFactory();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        var retryResponse = await _httpClient.SendAsync(retry);

        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            retryResponse.Dispose();
            Logger.Instance.Log("🟠 WARN SyncService retry also 401; clearing auth state");
            _authService.ClearLocalAuth();
            return null;
        }

        return retryResponse;
    }

    private static List<string> FetchLocalFiles(string directory, string? filter = null)
    {
        if (!Directory.Exists(directory))
            return [];

        var files = Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .Where(f => f != null && !f.StartsWith('.'))
            .Cast<string>();

        if (filter != null)
        {
            var extension = filter.Replace("*", "");
            files = files.Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        return files.ToList();
    }

    private static List<string> FetchLocalDirectories(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.GetDirectories(directory)
            .Select(Path.GetFileName)
            .Where(f => f != null && !f.StartsWith('.'))
            .Cast<string>()
            .ToList();
    }

    private static string MimeTypeFor(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant().TrimStart('.');
        return ext switch
        {
            "md" => "text/markdown",
            "url" => "text/plain",
            "txt" => "text/plain",
            "json" => "application/json",
            "toml" => "application/toml",
            "pdf" => "application/pdf",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "svg" => "image/svg+xml",
            "html" or "htm" => "text/html",
            _ => "application/octet-stream"
        };
    }
}
