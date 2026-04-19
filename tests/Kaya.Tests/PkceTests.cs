using System.Security.Cryptography;
using System.Text;
using Kaya.Core.Services;

namespace Kaya.Tests;

public class PkceTests
{
    [Fact]
    public void GenerateReturnsBase64UrlVerifierOfAtLeast43Chars()
    {
        var (verifier, _) = Pkce.Generate();

        Assert.True(verifier.Length >= 43, $"verifier too short: {verifier.Length}");
        Assert.DoesNotContain("=", verifier);
        Assert.DoesNotContain("+", verifier);
        Assert.DoesNotContain("/", verifier);
        foreach (var ch in verifier)
        {
            Assert.True(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_',
                $"unexpected char in verifier: {ch}");
        }
    }

    [Fact]
    public void GenerateReturnsChallengeOf43Base64UrlChars()
    {
        var (_, challenge) = Pkce.Generate();

        Assert.Equal(43, challenge.Length);
        Assert.DoesNotContain("=", challenge);
        Assert.DoesNotContain("+", challenge);
        Assert.DoesNotContain("/", challenge);
    }

    [Fact]
    public void ChallengeIsBase64UrlSha256OfVerifier()
    {
        var (verifier, challenge) = Pkce.Generate();

        var expectedBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var expected = Convert.ToBase64String(expectedBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Equal(expected, challenge);
    }

    [Fact]
    public void GenerateProducesDifferentVerifiersOnEachCall()
    {
        var (v1, _) = Pkce.Generate();
        var (v2, _) = Pkce.Generate();
        Assert.NotEqual(v1, v2);
    }
}
