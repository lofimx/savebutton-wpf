using System.Text.RegularExpressions;

namespace Kaya.Core.Models;

public partial class Filename
{
    private static readonly HashSet<string> ImageExtensions =
        ["png", "jpg", "jpeg", "gif", "webp", "svg", "bmp", "ico"];

    private static readonly HashSet<string> PdfExtensions = ["pdf"];

    private readonly string _value;

    public Filename(string filename) => _value = filename;

    public string Value => _value;

    public bool IsValid() => !InvalidCharsRegex().IsMatch(_value);

    public string Extension
    {
        get
        {
            var dotIndex = _value.LastIndexOf('.');
            return dotIndex >= 0 ? _value[(dotIndex + 1)..].ToLowerInvariant() : "";
        }
    }

    public bool IsImage() => ImageExtensions.Contains(Extension);

    public bool IsPdf() => PdfExtensions.Contains(Extension);

    public AngaType AngaType => Extension switch
    {
        "url" => AngaType.Bookmark,
        "md" => AngaType.Note,
        _ => AngaType.File
    };

    public string Date
    {
        get
        {
            var match = TimestampExtractRegex().Match(_value);
            return match.Success ? match.Groups[1].Value : "";
        }
    }

    public string RawTimestamp
    {
        get
        {
            var match = RawTimestampRegex().Match(_value);
            return match.Success ? match.Groups[1].Value : "";
        }
    }

    public string DisplayTitle
    {
        get
        {
            var withoutTimestamp = TimestampPrefixRegex().Replace(_value, "");
            // Files keep hyphens and extension verbatim
            if (AngaType == AngaType.File)
                return withoutTimestamp;
            // Notes and bookmarks: strip extension, replace hyphens with spaces
            var lastDot = withoutTimestamp.LastIndexOf('.');
            var withoutExtension = lastDot > 0 ? withoutTimestamp[..lastDot] : withoutTimestamp;
            return withoutExtension.Replace('-', ' ');
        }
    }

    [GeneratedRegex(@"[ #?&+=!*'()<>\[\]{}\""@^~`;\\|]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{6}(?:_\d{9})?-")]
    private static partial Regex TimestampPrefixRegex();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})T(\d{6})")]
    private static partial Regex TimestampExtractRegex();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}T\d{6}(?:_\d{9})?)")]
    private static partial Regex RawTimestampRegex();
}
