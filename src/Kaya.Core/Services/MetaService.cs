using System.Text.RegularExpressions;
using Kaya.Core.Models;

namespace Kaya.Core.Services;

public partial class MetaService
{
    private static readonly string LocalMetaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya", "meta");

    public MetaData? LoadLatestMeta(string angaFilename)
    {
        var metaFiles = ListMetaFiles();

        foreach (var metaFile in metaFiles)
        {
            var contents = ReadMetaFile(metaFile);
            var parsed = ParseMeta(contents);

            if (parsed.AngaFilename == angaFilename)
            {
                Logger.Instance.Log(
                    $"🔵 INFO MetaService found meta for \"{angaFilename}\" in \"{metaFile}\"");
                return new MetaData(parsed.Tags, parsed.Note);
            }
        }

        Logger.Instance.Log($"🟢 DEBUG MetaService no meta found for \"{angaFilename}\"");
        return null;
    }

    private static List<string> ListMetaFiles()
    {
        if (!Directory.Exists(LocalMetaDir))
            return [];

        return Directory.GetFiles(LocalMetaDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith('.') && name.EndsWith(".toml"))
            .Cast<string>()
            .OrderDescending()
            .ToList();
    }

    private static string ReadMetaFile(string filename)
    {
        try
        {
            var filePath = Path.Combine(LocalMetaDir, filename);
            return File.ReadAllText(filePath);
        }
        catch (Exception e)
        {
            Logger.Instance.Error($"🔴 ERROR MetaService failed to read {filename}: {e.Message}");
            return "";
        }
    }

    private static ParsedMeta ParseMeta(string tomlContent)
    {
        var filenameMatch = FilenameRegex().Match(tomlContent);
        var angaFilename = filenameMatch.Success ? filenameMatch.Groups[1].Value : "";

        var tags = new List<string>();
        var tagsMatch = TagsRegex().Match(tomlContent);
        if (tagsMatch.Success)
        {
            var tagMatches = TagValueRegex().Matches(tagsMatch.Groups[1].Value);
            foreach (Match m in tagMatches)
                tags.Add(m.Groups[1].Value);
        }

        var noteMatch = NoteRegex().Match(tomlContent);
        var note = noteMatch.Success ? noteMatch.Groups[1].Value : "";

        return new ParsedMeta(angaFilename, tags.ToArray(), note);
    }

    [GeneratedRegex(@"filename\s*=\s*""([^""]+)""")]
    private static partial Regex FilenameRegex();

    [GeneratedRegex(@"tags\s*=\s*\[([^\]]*)\]", RegexOptions.Singleline)]
    private static partial Regex TagsRegex();

    [GeneratedRegex(@"""([^""]+)""")]
    private static partial Regex TagValueRegex();

    [GeneratedRegex(@"note\s*=\s*'''([\s\S]*?)'''")]
    private static partial Regex NoteRegex();

    private record ParsedMeta(string AngaFilename, string[] Tags, string Note);
}

public record MetaData(string[] Tags, string Note);
