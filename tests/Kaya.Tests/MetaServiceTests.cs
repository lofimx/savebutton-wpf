using Kaya.Core.Services;

namespace Kaya.Tests;

public class MetaServiceTests
{
    [Fact]
    public void LoadLatestMeta_should_return_null_when_no_meta_directory()
    {
        // MetaService reads from the actual file system.
        // This test verifies it handles missing anga gracefully.
        var service = new MetaService();
        var result = service.LoadLatestMeta("nonexistent-file-that-does-not-exist.md");
        Assert.Null(result);
    }
}
