namespace Kaya.Core.Models;

public enum AngaType
{
    Bookmark,
    Note,
    File
}

public record SearchResult(
    string Filename,
    AngaType Type,
    string DisplayTitle,
    string ContentPreview,
    string Date,
    string RawTimestamp
);

public static class SearchResultFactory
{
    public static SearchResult FromFile(string filename, string contents)
    {
        var file = new Filename(filename);
        return new SearchResult(
            filename,
            file.AngaType,
            file.DisplayTitle,
            ExtractContentPreview(file.AngaType, contents),
            file.Date,
            file.RawTimestamp
        );
    }

    public static AngaType DetermineType(string filename) =>
        new Filename(filename).AngaType;

    public static bool IsTitleVisible(AngaType type) =>
        type == AngaType.File;

    public static string ExtractDisplayTitle(string filename) =>
        new Filename(filename).DisplayTitle;

    public static string ExtractContentPreview(AngaType type, string contents)
    {
        if (type == AngaType.Bookmark)
            return ExtractDomainFromUrl(contents);

        if (type == AngaType.Note)
        {
            const int maxPreviewLength = 100;
            return contents.Length > maxPreviewLength
                ? contents[..maxPreviewLength] + "..."
                : contents;
        }

        return "";
    }

    public static string ExtractDate(string filename) =>
        new Filename(filename).Date;

    public static string ExtractRawTimestamp(string filename) =>
        new Filename(filename).RawTimestamp;

    public static bool MatchesQuery(SearchResult result, string query)
    {
        var q = query.ToLowerInvariant();
        return result.Filename.ToLowerInvariant().Contains(q) ||
               result.DisplayTitle.ToLowerInvariant().Contains(q) ||
               result.ContentPreview.ToLowerInvariant().Contains(q);
    }

    private static string ExtractDomainFromUrl(string contents)
    {
        var lines = contents.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("URL="))
            {
                var urlString = line[4..].Trim();
                if (Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
                    return uri.Host;
                return urlString;
            }
        }
        return "";
    }
}
