using System.IO;
using Core.Pipeline;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class OutputPathResolverTests {
	[Fact]
	public void ResolveSourceDir_NullArgument_Throws() {
		var resolver = new OutputPathResolver(new TestLogger());

		var ex = Assert.Throws<InvalidArgumentException>(() => resolver.ResolveSourceDir(null));
		Assert.Contains("Invalid source files folder", ex.Message);
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
		var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
		try {
			var result = resolver.ResolveSourceDir(dir);
			Assert.Equal(dir.FullName, result.FullName);
		} finally {
			Directory.Delete(dir.FullName, recursive: true);
		}
	}

	[Fact]
	public void ResolveOutputPdf_CreatesParentDirectory_AndNormalizesExtension() {
		var logger = new TestLogger();
		var resolver = new OutputPathResolver(logger);
		var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		var requested = new FileInfo(Path.Combine(parent, "foo.txt"));
		try {
			var result = resolver.ResolveOutputPdf(requested);

			Assert.True(Directory.Exists(parent));
			Assert.Equal(Path.Combine(parent, "foo.pdf"), result.FullName);
		} finally {
			if (Directory.Exists(parent)) {
				Directory.Delete(parent, recursive: true);
			}
		}
	}

	[Fact]
	public void ResolveOutputPdf_NullArgument_Throws() {
		var resolver = new OutputPathResolver(new TestLogger());

		var ex = Assert.Throws<InvalidArgumentException>(() => resolver.ResolveOutputPdf(null));
		Assert.Contains("Invalid output file path", ex.Message);
	}
}
