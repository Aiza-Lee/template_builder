using System.IO;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class OutputPathResolverTests {
    [Fact]
    public void ResolveSourceDir_NullArgument_Throws() {
        var resolver = new OutputPathResolver(new TestLogger());

        var ex = Assert.Throws<InvalidArgumentException>(() => resolver.ResolveSourceDir(null));
        Assert.Contains("源文件目录无效", ex.Message);
    }

    [Fact]
    public void ResolveSourceDir_MissingFolder_Throws() {
        var resolver = new OutputPathResolver(new TestLogger());
        var fake = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var ex = Assert.Throws<InvalidArgumentException>(() => resolver.ResolveSourceDir(fake));
        Assert.Contains(fake.FullName, ex.Message);
    }

    [Fact]
    public void ResolveSourceDir_ExistingFolder_ReturnsIt() {
        var resolver = new OutputPathResolver(new TestLogger());
        using var tmp = TempDir.Create();

        var result = resolver.ResolveSourceDir(new DirectoryInfo(tmp.Path));

        Assert.Equal(tmp.Path, result.FullName);
    }

    [Fact]
    public void ResolveOutputPdf_CreatesParentDirectory_AndNormalizesExtension() {
        var logger = new TestLogger();
        var resolver = new OutputPathResolver(logger);
        using var tmp = TempDir.Create();
        var requested = new FileInfo(Path.Combine(tmp.Path, "foo.txt"));

        var result = resolver.ResolveOutputPdf(requested);

        Assert.True(Directory.Exists(tmp.Path));
        Assert.Equal(Path.Combine(tmp.Path, "foo.pdf"), result.FullName);
    }

    [Fact]
    public void ResolveOutputPdf_NullArgument_Throws() {
        var resolver = new OutputPathResolver(new TestLogger());

        var ex = Assert.Throws<InvalidArgumentException>(() => resolver.ResolveOutputPdf(null));
        Assert.Contains("输出文件路径无效", ex.Message);
    }
}
