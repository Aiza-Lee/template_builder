using Utils;
using Xunit;

namespace template_builder.Tests;

public class ManifestResourceManagerTests {
	[Fact]
	public void GetResourceInString_MainTemplate_ContainsContentPlaceholder() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		var mainTemplate = mgr.GetResourceInString("Templates.Main.tex");

		Assert.NotEmpty(mainTemplate);
		Assert.Contains("<<CONTENT>>", mainTemplate);
	}

	[Fact]
	public void GetResourceInString_CodeBlockTemplate_ContainsLanguagePlaceholder() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		var codeBlockTemplate = mgr.GetResourceInString("Templates.CodeBlock.tex");

		Assert.NotEmpty(codeBlockTemplate);
		Assert.Contains("<<LANGUAGE>>", codeBlockTemplate);
	}

	[Fact]
	public void GetResourceInString_UnknownResource_ReturnsEmptyAndLogsError() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		var content = mgr.GetResourceInString("NonExistent.Resource");

		Assert.Equal(string.Empty, content);
		Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR);
	}

	[Fact]
	public void GetResourceAsStream_MainTemplate_IsReadable() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		using var stream = mgr.GetResourceAsStream("Templates.Main.tex");
		using var reader = new StreamReader(stream);
		var content = reader.ReadToEnd();

		Assert.NotEmpty(content);
		Assert.Contains("<<CONTENT>>", content);
	}
}
