using Utils;
using Utils.Exceptions;
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
	public void GetResourceInString_UnknownResource_ThrowsMissingEmbeddedResourceException() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		var ex = Assert.Throws<MissingEmbeddedResourceException>(
			() => mgr.GetResourceInString("NonExistent.Resource")
		);
		Assert.Equal("NonExistent.Resource", ex.ResourceName);
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

	[Fact]
	public void GetResourceAsStream_UnknownResource_ThrowsMissingEmbeddedResourceException() {
		var logger = new TestLogger();
		var mgr = new ManifestResourceManager(logger);

		var ex = Assert.Throws<MissingEmbeddedResourceException>(
			() => mgr.GetResourceAsStream("NonExistent.Resource")
		);
		Assert.Equal("NonExistent.Resource", ex.ResourceName);
	}
}
