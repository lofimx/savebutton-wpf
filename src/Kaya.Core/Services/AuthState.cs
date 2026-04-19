namespace Kaya.Core.Services;

public static class AuthState
{
    public const string TokenMethod = "token";

    public static bool IsSignedIn(string? authMethod, string? authEmail) =>
        authMethod == TokenMethod && !string.IsNullOrEmpty(authEmail);
}
