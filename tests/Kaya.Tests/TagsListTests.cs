using Kaya.Core.Models;

namespace Kaya.Tests;

public class TagsListTests
{
    [Fact]
    public void Should_start_empty()
    {
        var tags = new TagsList();
        Assert.Empty(tags.Tags);
        Assert.Equal(0, tags.Length);
    }

    [Fact]
    public void Should_accept_initial_tags()
    {
        var tags = new TagsList(["foo", "bar"]);
        Assert.Equal(2, tags.Length);
        Assert.Equal(["foo", "bar"], tags.Tags);
    }

    [Fact]
    public void Should_not_mutate_initial_tags_array()
    {
        var initial = new List<string> { "podcast" };
        var tags = new TagsList(initial);
        tags.Add("democracy");
        Assert.Single(initial);
    }

    [Fact]
    public void WithPending_should_not_mutate_internal_tags()
    {
        var tags = new TagsList(["podcast"]);
        tags.WithPending("democracy");
        Assert.Equal(["podcast"], tags.Tags);
    }

    [Fact]
    public void Should_add_tags()
    {
        var tags = new TagsList();
        tags.Add("hello");
        tags.Add("world");
        Assert.Equal(2, tags.Length);
        Assert.Equal(["hello", "world"], tags.Tags);
    }

    [Fact]
    public void Should_remove_last_tag()
    {
        var tags = new TagsList(["a", "b", "c"]);
        var removed = tags.RemoveLast();
        Assert.Equal("c", removed);
        Assert.Equal(2, tags.Length);
        Assert.Equal(["a", "b"], tags.Tags);
    }

    [Fact]
    public void Should_return_null_when_removing_from_empty()
    {
        var tags = new TagsList();
        Assert.Null(tags.RemoveLast());
    }

    [Fact]
    public void WithPending_should_include_trimmed_pending_text()
    {
        var tags = new TagsList(["a", "b"]);
        var result = tags.WithPending("  c  ");
        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void WithPending_should_exclude_empty_pending_text()
    {
        var tags = new TagsList(["a", "b"]);
        var result = tags.WithPending("   ");
        Assert.Equal(["a", "b"], result);
    }

    [Fact]
    public void Tags_property_should_return_copy()
    {
        var tags = new TagsList(["a"]);
        var list = tags.Tags;
        tags.Add("b");
        Assert.Single(list);
    }
}
