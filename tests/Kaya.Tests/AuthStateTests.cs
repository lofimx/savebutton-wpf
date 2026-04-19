using Kaya.Core.Services;

namespace Kaya.Tests;

public class AuthStateTests
{
    [Fact]
    public void SignedInRequiresTokenMethodAndNonEmptyEmail()
    {
        Assert.True(AuthState.IsSignedIn("token", "user@example.com"));
    }

    [Fact]
    public void EmptyEmailIsNotSignedIn()
    {
        Assert.False(AuthState.IsSignedIn("token", ""));
    }

    [Fact]
    public void NullEmailIsNotSignedIn()
    {
        Assert.False(AuthState.IsSignedIn("token", null));
    }

    [Fact]
    public void NonTokenMethodIsNotSignedIn()
    {
        Assert.False(AuthState.IsSignedIn("basic", "user@example.com"));
        Assert.False(AuthState.IsSignedIn("", "user@example.com"));
        Assert.False(AuthState.IsSignedIn(null, "user@example.com"));
    }
}
