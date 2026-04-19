namespace Kaya.Core.Models;

public record ProviderInfo(string IconKey, string Label, string BrandColor)
{
    public static ProviderInfo ForIdentityProvider(string? identityProvider)
    {
        return identityProvider switch
        {
            "google_oauth2" => new ProviderInfo("icon_google", "Google", "#4285F4"),
            "microsoft_graph" => new ProviderInfo("icon_microsoft", "Microsoft", "#0078D4"),
            "apple" => new ProviderInfo("icon_apple", "Apple", "#000000"),
            _ => new ProviderInfo("icon_email", "Email", "#6E6E6E"),
        };
    }
}
