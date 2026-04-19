using Kaya.Core.Models;

namespace Kaya.Tests;

public class ProviderIconTests
{
    [Fact]
    public void GoogleIdentityMapsToGoogleInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("google_oauth2");
        Assert.Equal("Google", info.Label);
        Assert.Equal("icon_google", info.IconKey);
    }

    [Fact]
    public void MicrosoftIdentityMapsToMicrosoftInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("microsoft_graph");
        Assert.Equal("Microsoft", info.Label);
        Assert.Equal("icon_microsoft", info.IconKey);
    }

    [Fact]
    public void AppleIdentityMapsToAppleInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("apple");
        Assert.Equal("Apple", info.Label);
        Assert.Equal("icon_apple", info.IconKey);
    }

    [Fact]
    public void EmptyIdentityMapsToEmailInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("");
        Assert.Equal("Email", info.Label);
        Assert.Equal("icon_email", info.IconKey);
    }

    [Fact]
    public void NullIdentityMapsToEmailInfo()
    {
        var info = ProviderInfo.ForIdentityProvider(null);
        Assert.Equal("Email", info.Label);
        Assert.Equal("icon_email", info.IconKey);
    }

    [Fact]
    public void PasswordIdentityMapsToEmailInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("password");
        Assert.Equal("Email", info.Label);
        Assert.Equal("icon_email", info.IconKey);
    }

    [Fact]
    public void UnknownIdentityFallsBackToEmailInfo()
    {
        var info = ProviderInfo.ForIdentityProvider("someone_new");
        Assert.Equal("Email", info.Label);
        Assert.Equal("icon_email", info.IconKey);
    }
}
