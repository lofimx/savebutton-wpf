using AdysTech.CredentialManager;
using System.Net;

namespace Kaya.Core.Services;

public class CredentialService
{
    private const string RefreshTokenTarget = "Save Button Refresh Token";

    // Legacy target names from pre-OAuth builds; silently removed at startup.
    private static readonly string[] LegacyTargets = ["Kaya Server Password"];

    public string? GetRefreshToken()
    {
        try
        {
            var credential = CredentialManager.GetCredentials(RefreshTokenTarget);
            return credential?.Password;
        }
        catch
        {
            return null;
        }
    }

    public bool SetRefreshToken(string token)
    {
        try
        {
            var credential = new NetworkCredential(RefreshTokenTarget, token);
            CredentialManager.SaveCredentials(RefreshTokenTarget, credential);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ClearRefreshToken()
    {
        try
        {
            CredentialManager.RemoveCredentials(RefreshTokenTarget);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RemoveLegacyCredentials()
    {
        foreach (var target in LegacyTargets)
        {
            try
            {
                CredentialManager.RemoveCredentials(target);
                Logger.Instance.Log($"🔵 INFO CredentialService removed legacy credential: {target}");
            }
            catch
            {
                // Not present — fine.
            }
        }
    }
}
