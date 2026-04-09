namespace Kaya.Core.Models;

public abstract record NativeHostRequestType
{
    public record Health : NativeHostRequestType;
    public record Preflight : NativeHostRequestType;
    public record Config : NativeHostRequestType;
    public record Listing(string Collection) : NativeHostRequestType;
    public record FileWrite(string Collection, string Filename) : NativeHostRequestType;
    public record Invalid(string Reason) : NativeHostRequestType;
}

public class NativeHostRequest
{
    private static readonly HashSet<string> Collections = ["anga", "meta", "words"];

    public NativeHostRequestType Type { get; }

    public NativeHostRequest(string method, string path)
    {
        Type = Parse(method, path);
    }

    private static NativeHostRequestType Parse(string method, string path)
    {
        if (method == "OPTIONS")
            return new NativeHostRequestType.Preflight();

        var segments = path.TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return new NativeHostRequestType.Invalid("Empty path");

        var resource = segments[0];

        if (method == "GET" && resource == "health" && segments.Length == 1)
            return new NativeHostRequestType.Health();

        if (method == "POST" && resource == "config" && segments.Length == 1)
            return new NativeHostRequestType.Config();

        if (!Collections.Contains(resource))
            return new NativeHostRequestType.Invalid($"Unknown resource: {resource}");

        if (resource == "words")
            return ParseWords(method, segments);

        // anga or meta
        if (segments.Length == 1 && method == "GET")
            return new NativeHostRequestType.Listing(resource);

        if (segments.Length == 2 && method == "POST")
        {
            var filename = DecodeAndValidateFilename(segments[1]);
            if (filename is NativeHostRequestType error)
                return error;
            return new NativeHostRequestType.FileWrite(resource, (string)filename);
        }

        return new NativeHostRequestType.Invalid($"Invalid {method} on /{resource}");
    }

    private static NativeHostRequestType ParseWords(string method, string[] segments)
    {
        // GET /words — list word subdirectories
        if (segments.Length == 1 && method == "GET")
            return new NativeHostRequestType.Listing("words");

        // GET /words/{anga} — list files in that word subdirectory
        if (segments.Length == 2 && method == "GET")
        {
            var dir = DecodeAndValidateDirectoryName(segments[1]);
            if (dir is NativeHostRequestType error)
                return error;
            return new NativeHostRequestType.Listing($"words/{(string)dir}");
        }

        // POST /words/{anga}/{filename} — write a words file
        if (segments.Length == 3 && method == "POST")
        {
            var dir = DecodeAndValidateDirectoryName(segments[1]);
            if (dir is NativeHostRequestType error1)
                return error1;
            var filename = DecodeAndValidateFilename(segments[2]);
            if (filename is NativeHostRequestType error2)
                return error2;
            return new NativeHostRequestType.FileWrite($"words/{(string)dir}", (string)filename);
        }

        return new NativeHostRequestType.Invalid($"Invalid {method} on /words");
    }

    private static object DecodeAndValidateFilename(string raw)
    {
        var decoded = Uri.UnescapeDataString(raw);

        if (decoded.Length == 0)
            return new NativeHostRequestType.Invalid("Empty filename");
        if (decoded.Contains('/'))
            return new NativeHostRequestType.Invalid("Filename contains /");
        if (decoded.Contains(".."))
            return new NativeHostRequestType.Invalid("Filename contains ..");

        return decoded;
    }

    private static object DecodeAndValidateDirectoryName(string raw)
    {
        var decoded = Uri.UnescapeDataString(raw);

        if (decoded.Length == 0)
            return new NativeHostRequestType.Invalid("Empty directory name");
        if (decoded.Contains(".."))
            return new NativeHostRequestType.Invalid("Directory name contains ..");

        return decoded;
    }
}
