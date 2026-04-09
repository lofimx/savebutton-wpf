using Kaya.Core.Models;

namespace Kaya.Tests;

public class FilenameExtendedTests
{
    [Theory]
    [InlineData("photo.png", "png")]
    [InlineData("document.PDF", "pdf")]
    [InlineData("archive.tar.gz", "gz")]
    [InlineData("noext", "")]
    public void Extension_should_return_lowercase_extension(string filename, string expected)
    {
        Assert.Equal(expected, new Filename(filename).Extension);
    }

    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("image.jpg", true)]
    [InlineData("pic.jpeg", true)]
    [InlineData("anim.gif", true)]
    [InlineData("pic.webp", true)]
    [InlineData("icon.svg", true)]
    [InlineData("icon.bmp", true)]
    [InlineData("icon.ico", true)]
    [InlineData("document.pdf", false)]
    [InlineData("file.txt", false)]
    public void IsImage_should_detect_image_extensions(string filename, bool expected)
    {
        Assert.Equal(expected, new Filename(filename).IsImage());
    }

    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("photo.png", false)]
    [InlineData("file.txt", false)]
    public void IsPdf_should_detect_pdf_extension(string filename, bool expected)
    {
        Assert.Equal(expected, new Filename(filename).IsPdf());
    }

    [Theory]
    [InlineData("2025-06-28T120000-bookmark.url", AngaType.Bookmark)]
    [InlineData("2025-06-28T120000-note.md", AngaType.Note)]
    [InlineData("2025-06-28T120000-photo.png", AngaType.File)]
    [InlineData("2025-06-28T120000-document.pdf", AngaType.File)]
    public void AngaType_should_be_determined_by_extension(string filename, AngaType expected)
    {
        Assert.Equal(expected, new Filename(filename).AngaType);
    }

    [Fact]
    public void Date_should_extract_date_from_timestamp()
    {
        Assert.Equal("2025-06-28", new Filename("2025-06-28T120000-test.md").Date);
    }

    [Fact]
    public void Date_should_return_empty_for_no_timestamp()
    {
        Assert.Equal("", new Filename("no-timestamp.txt").Date);
    }

    [Fact]
    public void RawTimestamp_should_extract_full_timestamp()
    {
        Assert.Equal("2025-06-28T120000", new Filename("2025-06-28T120000-test.md").RawTimestamp);
    }

    [Fact]
    public void RawTimestamp_should_include_nanos()
    {
        Assert.Equal("2025-06-28T120000_123456789",
            new Filename("2025-06-28T120000_123456789-test.md").RawTimestamp);
    }

    [Fact]
    public void DisplayTitle_for_note_should_strip_timestamp_extension_replace_hyphens()
    {
        Assert.Equal("my cool note",
            new Filename("2025-06-28T120000-my-cool-note.md").DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_for_bookmark_should_strip_timestamp_extension_replace_hyphens()
    {
        Assert.Equal("example com",
            new Filename("2025-06-28T120000-example-com.url").DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_for_file_should_keep_hyphens_and_extension()
    {
        Assert.Equal("my-photo.png",
            new Filename("2025-06-28T120000-my-photo.png").DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_for_file_with_dots_should_preserve_them()
    {
        Assert.Equal("archive.tar.gz",
            new Filename("2025-06-28T120000-archive.tar.gz").DisplayTitle);
    }
}
