using Kaya.Core.Services;

namespace Kaya.Tests;

public class ServerUrlHelperTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com//", "https://example.com")]
    [InlineData("https://example.com///", "https://example.com")]
    [InlineData("  https://example.com/  ", "https://example.com")]
    public void NormalizeStripsTrailingSlashesAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, ServerUrlHelper.Normalize(input));
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost")]
    [InlineData("https://127.0.0.1:8080")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://10.255.255.255")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.0.1:3000/")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.20.3.4")]
    [InlineData("http://172.31.255.255")]
    public void IsPrivateHostMatchesLocalhostAndPrivateRanges(string url)
    {
        Assert.True(ServerUrlHelper.IsPrivateHost(url), $"expected private: {url}");
    }

    [Theory]
    [InlineData("https://savebutton.com")]
    [InlineData("https://example.com")]
    [InlineData("https://1.2.3.4")]
    [InlineData("http://172.15.0.1")]
    [InlineData("http://172.32.0.1")]
    [InlineData("http://11.0.0.1")]
    [InlineData("http://9.0.0.1")]
    [InlineData("https://localhostilo.com")]
    public void IsPrivateHostReturnsFalseForPublicOrBoundary(string url)
    {
        Assert.False(ServerUrlHelper.IsPrivateHost(url), $"expected public: {url}");
    }

    [Fact]
    public void EmptyUrlIsNotPrivate()
    {
        Assert.False(ServerUrlHelper.IsPrivateHost(""));
        Assert.False(ServerUrlHelper.IsPrivateHost(null));
    }
}
