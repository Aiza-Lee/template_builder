using System.IO;
using Core.Pipeline;
using Utils;
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
			Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("Using configuration file"));
		} finally {
			if (tmp.Exists) tmp.Delete();
		}
	}

	[Fact]
	public void Resolve_MissingRequested_FallsBackAndMarksNotUserProvided() {
		var logger = new TestLogger();
		var resolver = new ConfigPathResolver(logger);

		var missing = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));
		var fallback = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

		var (result, userProvided) = resolver.Resolve(missing, userProvidedAtCli: true, fallback);

		Assert.Equal(fallback.FullName, result.FullName);
		Assert.False(userProvided); // 回退后不再严格
		Assert.Contains(logger.Entries, e => e.Level == LogLevel.WARNING && e.Message.Contains("not found"));
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
}
