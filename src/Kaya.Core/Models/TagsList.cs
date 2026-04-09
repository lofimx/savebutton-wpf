namespace Kaya.Core.Models;

public class TagsList
{
    private readonly List<string> _tags = [];

    public TagsList(IEnumerable<string>? initialTags = null)
    {
        if (initialTags is not null)
            _tags.AddRange(initialTags);
    }

    public IReadOnlyList<string> Tags => _tags.ToList();

    public int Length => _tags.Count;

    public void Add(string tag) => _tags.Add(tag);

    public string? RemoveLast()
    {
        if (_tags.Count == 0) return null;
        var last = _tags[^1];
        _tags.RemoveAt(_tags.Count - 1);
        return last;
    }

    public string[] WithPending(string pendingText)
    {
        var trimmed = pendingText.Trim();
        return trimmed.Length > 0
            ? [.. _tags, trimmed]
            : [.. _tags];
    }
}
