using Kaya.Core.Models;

namespace Kaya.Tests;

public class SearchResultTests
{
    public class DetermineTypeTests
    {
        [Fact]
        public void Should_identify_url_files_as_bookmarks()
        {
            Assert.Equal(AngaType.Bookmark,
                SearchResultFactory.DetermineType("2025-06-28T120000-bookmark.url"));
        }

        [Fact]
        public void Should_identify_md_files_as_blurbs()
        {
            Assert.Equal(AngaType.Blurb,
                SearchResultFactory.DetermineType("2025-06-28T120000-blurb.md"));
        }

        [Fact]
        public void Should_classify_legacy_note_md_files_as_blurbs()
        {
            // Existing user data created before the "note" → "blurb" rename
            // uses the -note.md slug. The .md extension is the canonical
            // signal — the slug is decorative — so legacy filenames must
            // continue to be classified as blurbs.
            Assert.Equal(AngaType.Blurb,
                SearchResultFactory.DetermineType("2024-01-01T120000-note.md"));
        }

        [Fact]
        public void Should_classify_quote_md_files_as_blurbs()
        {
            // The wxt extension's context menu writes -quote.md for text
            // selections. These must also be classified as blurbs.
            Assert.Equal(AngaType.Blurb,
                SearchResultFactory.DetermineType("2024-01-01T120000-quote.md"));
        }

        [Fact]
        public void Should_identify_other_extensions_as_files()
        {
            Assert.Equal(AngaType.File,
                SearchResultFactory.DetermineType("2025-06-28T120000-photo.png"));
        }
    }

    public class ExtractDisplayTitleTests
    {
        [Fact]
        public void Should_strip_timestamp_and_extension_replacing_hyphens_with_spaces()
        {
            Assert.Equal("my important blurb",
                SearchResultFactory.ExtractDisplayTitle("2025-06-28T120000-my-important-blurb.md"));
        }

        [Fact]
        public void Should_handle_nanosecond_timestamps()
        {
            Assert.Equal("blurb",
                SearchResultFactory.ExtractDisplayTitle("2026-01-21T164145_354000000-blurb.md"));
        }

        [Fact]
        public void Should_preserve_hyphens_and_extension_for_files()
        {
            Assert.Equal("my-file.tar.gz",
                SearchResultFactory.ExtractDisplayTitle("2025-06-28T120000-my-file.tar.gz"));
        }
    }

    public class ExtractDateTests
    {
        [Fact]
        public void Should_extract_date_from_filename_timestamp()
        {
            Assert.Equal("2025-06-28",
                SearchResultFactory.ExtractDate("2025-06-28T120000-blurb.md"));
        }

        [Fact]
        public void Should_extract_date_from_nanosecond_timestamp()
        {
            Assert.Equal("2026-01-21",
                SearchResultFactory.ExtractDate("2026-01-21T164145_354000000-blurb.md"));
        }
    }

    public class FromFileTests
    {
        [Fact]
        public void Should_create_a_bookmark_with_domain_preview()
        {
            var result = SearchResultFactory.FromFile(
                "2025-06-28T120000-bookmark.url",
                "[InternetShortcut]\nURL=https://example.com/path\n");

            Assert.Equal(AngaType.Bookmark, result.Type);
            Assert.Equal("example.com", result.ContentPreview);
            Assert.Equal("bookmark", result.DisplayTitle);
            Assert.Equal("2025-06-28", result.Date);
        }

        [Fact]
        public void Should_create_a_blurb_with_text_preview()
        {
            var result = SearchResultFactory.FromFile(
                "2025-06-28T120000-blurb.md",
                "Hello world, this is my blurb");

            Assert.Equal(AngaType.Blurb, result.Type);
            Assert.Equal("Hello world, this is my blurb", result.ContentPreview);
            Assert.Equal("blurb", result.DisplayTitle);
        }

        [Fact]
        public void Should_truncate_long_blurb_previews()
        {
            var longText = new string('a', 150);
            var result = SearchResultFactory.FromFile(
                "2025-06-28T120000-blurb.md", longText);

            Assert.Equal(new string('a', 100) + "...", result.ContentPreview);
        }

        [Fact]
        public void Should_create_a_file_with_empty_preview()
        {
            var result = SearchResultFactory.FromFile(
                "2025-06-28T120000-photo.png", "");

            Assert.Equal(AngaType.File, result.Type);
            Assert.Equal("", result.ContentPreview);
        }
    }

    public class MatchesQueryTests
    {
        private readonly SearchResult _result = SearchResultFactory.FromFile(
            "2025-06-28T120000-bookmark.url",
            "[InternetShortcut]\nURL=https://example.com\n");

        [Fact]
        public void Should_match_on_filename()
        {
            Assert.True(SearchResultFactory.MatchesQuery(_result, "bookmark"));
        }

        [Fact]
        public void Should_match_on_display_title()
        {
            Assert.True(SearchResultFactory.MatchesQuery(_result, "bookmark"));
        }

        [Fact]
        public void Should_match_on_content_preview()
        {
            Assert.True(SearchResultFactory.MatchesQuery(_result, "example.com"));
        }

        [Fact]
        public void Should_be_case_insensitive()
        {
            Assert.True(SearchResultFactory.MatchesQuery(_result, "EXAMPLE"));
        }

        [Fact]
        public void Should_return_false_for_non_matching_query()
        {
            Assert.False(SearchResultFactory.MatchesQuery(_result, "zzzzz"));
        }
    }
}
