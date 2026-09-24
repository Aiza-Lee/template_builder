using System.IO;
using Core.Pipeline;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ConfigPathResolverTests {
    [Fact]
    public void Resolve_ExistingFileFromCli_KeepsUserProvidedFlag() {
        var logger = new TestLogger();
        var resolver = new ConfigPathResolver(logger);

        var tmp = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
        try {
            File.WriteAllText(tmp.FullName, "{}");
            tmp.Refresh();

            var fallback = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
            var (result, userProvided) = resolver.Resolve(tmp, userProvidedAtCli: true, fallback);

            Assert.Equal(tmp.FullName, result.FullName);
            Assert.True(userProvided);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("使用配置文件"));
        } finally {
            if (tmp.Exists) tmp.Delete();
        }
    }

    [Fact]
    public void Resolve_MissingRequestedWhenUserProvided_ThrowsInvalidArgumentException() {
        var logger = new TestLogger();
        var resolver = new ConfigPathResolver(logger);

        var missing = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
        var fallback = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        var ex = Assert.Throws<InvalidArgumentException>(() =>
            resolver.Resolve(missing, userProvidedAtCli: true, fallback));

        Assert.Contains(missing.FullName, ex.Message);
        Assert.Contains("不存在", ex.Message);
    }

    [Fact]
    public void Resolve_MissingRequestedWhenNotUserProvided_FallsBackAndMarksNotUserProvided() {
        var logger = new TestLogger();
        var resolver = new ConfigPathResolver(logger);

        var missing = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
        var fallback = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        var (result, userProvided) = resolver.Resolve(missing, userProvidedAtCli: false, fallback);

        Assert.Equal(fallback.FullName, result.FullName);
        Assert.False(userProvided);
    }

    [Fact]
    public void Resolve_NullRequested_FallsBack() {
        var logger = new TestLogger();
        var resolver = new ConfigPathResolver(logger);
        var fallback = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        var (result, userProvided) = resolver.Resolve(null, userProvidedAtCli: false, fallback);

        Assert.Equal(fallback.FullName, result.FullName);
        Assert.False(userProvided);
    }

    [Fact]
    public void Resolve_ExistingFileFromCli_DoesNotInvokeFallbackFactory() {
        var logger = new TestLogger();
        var resolver = new ConfigPathResolver(logger);

        var tmp = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
        try {
            File.WriteAllText(tmp.FullName, "{}");
            tmp.Refresh();

            bool fallbackFactoryCalled = false;
            var (result, userProvided) = resolver.Resolve(
                tmp,
                userProvidedAtCli: true,
                defaultFallbackFactory: () => {
                    fallbackFactoryCalled = true;
                    return tmp;
                }
            );

            Assert.Equal(tmp.FullName, result.FullName);
            Assert.True(userProvided);
            Assert.False(fallbackFactoryCalled, "用户提供有效配置时，不应调用回退工厂。");
        } finally {
            if (tmp.Exists) tmp.Delete();
        }
    }
}
