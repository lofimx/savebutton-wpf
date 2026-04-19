using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Kaya.Core.Services;

namespace Kaya.Tests;

public class NativeHostConfigDeprecationTests : IDisposable
{
    private readonly NativeHostServer _server;
    private readonly SettingsService _settingsService;
    private readonly CredentialService _credentialService;
    private readonly HttpClient _client;
    private readonly int _port;
    private readonly string _tempHome;

    public NativeHostConfigDeprecationTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), $"kaya-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempHome);
        Environment.SetEnvironmentVariable("USERPROFILE", _tempHome);

        _port = GetFreePort();
        _settingsService = new SettingsService();
        _settingsService.NativeHostPort = _port;
        _credentialService = new CredentialService();

        _server = new NativeHostServer(_settingsService, _credentialService);
        _server.Start();
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}/") };
    }

    [Fact]
    public async Task ConfigPostReturns410Gone()
    {
        var payload = "{\"server\":\"https://evil.example.com\",\"email\":\"e@v.il\",\"password\":\"p\"}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("config", content);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task ConfigPostSendsDeprecationHeader()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("config", content);

        Assert.True(response.Headers.Contains("Deprecation"));
        Assert.Equal("true", string.Join(",", response.Headers.GetValues("Deprecation")));
    }

    [Fact]
    public async Task ConfigPostDoesNotTouchSettings()
    {
        var originalServerUrl = _settingsService.ServerUrl;
        var originalEmail = _settingsService.Email;

        var payload = "{\"server\":\"https://evil.example.com\",\"email\":\"e@v.il\",\"password\":\"p\"}";
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        await _client.PostAsync("config", content);

        Assert.Equal(originalServerUrl, _settingsService.ServerUrl);
        Assert.Equal(originalEmail, _settingsService.Email);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _server.Stop();
        _client.Dispose();
        try { Directory.Delete(_tempHome, recursive: true); } catch { }
    }
}
