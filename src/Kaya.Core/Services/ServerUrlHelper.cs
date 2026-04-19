namespace Kaya.Core.Services;

public static class ServerUrlHelper
{
    public static string Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        return url.Trim().TrimEnd('/');
    }

    public static bool IsPrivateHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        var host = ExtractHost(url);
        if (string.IsNullOrEmpty(host)) return false;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.StartsWith("127.")) return true;
        if (host.StartsWith("10.")) return true;
        if (host.StartsWith("192.168.")) return true;

        if (host.StartsWith("172."))
        {
            var segments = host.Split('.');
            if (segments.Length >= 2 && int.TryParse(segments[1], out var second))
                if (second >= 16 && second <= 31) return true;
        }

        return false;
    }

    private static string ExtractHost(string url)
    {
        var normalized = url.Trim();
        var schemeIdx = normalized.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx >= 0)
            normalized = normalized[(schemeIdx + 3)..];

        var pathIdx = normalized.IndexOfAny(['/', '?', '#']);
        if (pathIdx >= 0)
            normalized = normalized[..pathIdx];

        var portIdx = normalized.IndexOf(':');
        if (portIdx >= 0)
            normalized = normalized[..portIdx];

        return normalized;
    }
}
