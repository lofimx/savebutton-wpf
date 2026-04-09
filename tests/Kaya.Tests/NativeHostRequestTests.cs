using Kaya.Core.Models;

namespace Kaya.Tests;

public class NativeHostRequestTests
{
    public class HealthTests
    {
        [Fact]
        public void Should_parse_GET_health()
        {
            var req = new NativeHostRequest("GET", "/health");
            Assert.IsType<NativeHostRequestType.Health>(req.Type);
        }

        [Fact]
        public void Should_reject_POST_health()
        {
            var req = new NativeHostRequest("POST", "/health");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }
    }

    public class PreflightTests
    {
        [Fact]
        public void Should_parse_OPTIONS_on_any_path()
        {
            var req = new NativeHostRequest("OPTIONS", "/anga/somefile.url");
            Assert.IsType<NativeHostRequestType.Preflight>(req.Type);
        }

        [Fact]
        public void Should_parse_OPTIONS_on_root()
        {
            var req = new NativeHostRequest("OPTIONS", "/");
            Assert.IsType<NativeHostRequestType.Preflight>(req.Type);
        }
    }

    public class ConfigTests
    {
        [Fact]
        public void Should_parse_POST_config()
        {
            var req = new NativeHostRequest("POST", "/config");
            Assert.IsType<NativeHostRequestType.Config>(req.Type);
        }

        [Fact]
        public void Should_reject_GET_config()
        {
            var req = new NativeHostRequest("GET", "/config");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }
    }

    public class ListingTests
    {
        [Fact]
        public void Should_parse_GET_anga()
        {
            var req = new NativeHostRequest("GET", "/anga");
            var listing = Assert.IsType<NativeHostRequestType.Listing>(req.Type);
            Assert.Equal("anga", listing.Collection);
        }

        [Fact]
        public void Should_parse_GET_meta()
        {
            var req = new NativeHostRequest("GET", "/meta");
            var listing = Assert.IsType<NativeHostRequestType.Listing>(req.Type);
            Assert.Equal("meta", listing.Collection);
        }

        [Fact]
        public void Should_parse_GET_words()
        {
            var req = new NativeHostRequest("GET", "/words");
            var listing = Assert.IsType<NativeHostRequestType.Listing>(req.Type);
            Assert.Equal("words", listing.Collection);
        }

        [Fact]
        public void Should_parse_GET_words_anga_as_nested_listing()
        {
            var req = new NativeHostRequest("GET", "/words/2026-01-27T171207-www-deobald-ca");
            var listing = Assert.IsType<NativeHostRequestType.Listing>(req.Type);
            Assert.Equal("words/2026-01-27T171207-www-deobald-ca", listing.Collection);
        }

        [Fact]
        public void Should_reject_POST_anga_listing_is_GET_only()
        {
            var req = new NativeHostRequest("POST", "/anga");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }
    }

    public class FileWriteTests
    {
        [Fact]
        public void Should_parse_POST_anga_filename()
        {
            var req = new NativeHostRequest("POST", "/anga/2026-01-27T171207-www-deobald-ca.url");
            var fw = Assert.IsType<NativeHostRequestType.FileWrite>(req.Type);
            Assert.Equal("anga", fw.Collection);
            Assert.Equal("2026-01-27T171207-www-deobald-ca.url", fw.Filename);
        }

        [Fact]
        public void Should_parse_POST_meta_filename()
        {
            var req = new NativeHostRequest("POST", "/meta/2026-01-27T171207.toml");
            var fw = Assert.IsType<NativeHostRequestType.FileWrite>(req.Type);
            Assert.Equal("meta", fw.Collection);
            Assert.Equal("2026-01-27T171207.toml", fw.Filename);
        }

        [Fact]
        public void Should_parse_POST_words_anga_filename()
        {
            var req = new NativeHostRequest("POST", "/words/2026-01-27T171207-www-deobald-ca/plaintext.txt");
            var fw = Assert.IsType<NativeHostRequestType.FileWrite>(req.Type);
            Assert.Equal("words/2026-01-27T171207-www-deobald-ca", fw.Collection);
            Assert.Equal("plaintext.txt", fw.Filename);
        }

        [Fact]
        public void Should_URL_decode_filenames()
        {
            var req = new NativeHostRequest("POST", "/anga/2025-01-01T120000-India%20Income%20Tax.pdf");
            var fw = Assert.IsType<NativeHostRequestType.FileWrite>(req.Type);
            Assert.Equal("2025-01-01T120000-India Income Tax.pdf", fw.Filename);
        }

        [Fact]
        public void Should_reject_GET_anga_filename_write_is_POST_only()
        {
            var req = new NativeHostRequest("GET", "/anga/2026-01-27T171207-www-deobald-ca.url");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }
    }

    public class ValidationTests
    {
        [Fact]
        public void Should_reject_empty_filenames()
        {
            var req = new NativeHostRequest("POST", "/anga/");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }

        [Fact]
        public void Should_reject_filenames_with_directory_traversal()
        {
            var req = new NativeHostRequest("POST", "/anga/..%2F..%2Fetc%2Fpasswd");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }

        [Fact]
        public void Should_reject_filenames_containing_slash()
        {
            var req = new NativeHostRequest("POST", "/anga/sub%2Ffile.txt");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }

        [Fact]
        public void Should_reject_words_directory_names_with_dotdot()
        {
            var req = new NativeHostRequest("POST", "/words/../etc/passwd");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }

        [Fact]
        public void Should_reject_unknown_resources()
        {
            var req = new NativeHostRequest("GET", "/unknown");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }

        [Fact]
        public void Should_reject_empty_path()
        {
            var req = new NativeHostRequest("GET", "/");
            Assert.IsType<NativeHostRequestType.Invalid>(req.Type);
        }
    }
}
