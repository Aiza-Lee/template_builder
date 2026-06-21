using System;
using System.IO;
using System.Text;
using Core;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class PdfBuilderTests {
	[Fact]
	public void Cleanup_RemovesAllAuxiliaryExtensionsAndMidOutput() {
		var tempDir = CreateTempDir();
		var baseName = "book";

		// 模拟 xelatex 编译留下的中间文件
		var auxExts = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };
		foreach (var ext in auxExts) {
			File.WriteAllText(Path.Combine(tempDir, baseName + ext), "stub");
		}
		File.WriteAllText(Path.Combine(tempDir, "mid-output.tex"), "stub");
		// 一个无关文件，断言它不会被误删
		var keepMe = Path.Combine(tempDir, "book.pdf");
		File.WriteAllText(keepMe, "%PDF-1.4 stub");

		try {
			var logger = new TestLogger();
			PdfBuilder.Cleanup(tempDir, baseName, logger);

			foreach (var ext in auxExts) {
				Assert.False(
					File.Exists(Path.Combine(tempDir, baseName + ext)),
					$"Expected {baseName}{ext} to be deleted");
			}
			Assert.False(File.Exists(Path.Combine(tempDir, "mid-output.tex")));
			Assert.True(File.Exists(keepMe), "book.pdf should NOT be deleted");
		} finally {
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void Cleanup_HandlesMissingFilesGracefully() {
		var tempDir = CreateTempDir();
		try {
			var logger = new TestLogger();
			// 目录里什么也不创建，直接调用 Cleanup 不应抛异常
			var ex = Record.Exception(() => PdfBuilder.Cleanup(tempDir, "book", logger));
			Assert.Null(ex);
		} finally {
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void SaveTexFile_WritesContentAndReturnsPath() {
		var tempDir = CreateTempDir();
		try {
			const string content = "\\section{Hello}\nSome TeX body";

			var path = PdfBuilder.SaveTexFile(content, tempDir);

			Assert.Equal(Path.Combine(tempDir, "mid-output.tex"), path);
			Assert.Equal(content, File.ReadAllText(path));
		} finally {
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Theory]
	[InlineData("hello world", false)]
	[InlineData("##AUTHOR##", true)]
	[InlineData("<<CONTENT>>", true)]
	[InlineData("Mixed ##GEOMETRY_PAPER_SIZE## and <<MINTED_OUTPUTDIR>>", true)]
	[InlineData("lower <<wrong>> case", false)]
	[InlineData("c++ code: cout << x >> y;", false)]
	public void TemplatePlaceholderScanner_FindsUnresolvedMarkers(string content, bool expectMatch) {
		var matches = TemplatePlaceholderScanner.FindUnresolved(content).ToList();
		if (expectMatch) {
			Assert.NotEmpty(matches);
		} else {
			Assert.Empty(matches);
		}
	}

	[Fact]
	public void AppendLabeledStderr_PrefixesPassLabelAndPreservesStderr() {
		var buffer = new StringBuilder();
		PdfBuilder.AppendLabeledStderr(buffer, 1, "first pass stderr");
		PdfBuilder.AppendLabeledStderr(buffer, 2, "second pass stderr");
		var output = buffer.ToString();

		Assert.Contains("--- pass 1 stderr ---", output);
		Assert.Contains("first pass stderr", output);
		Assert.Contains("--- pass 2 stderr ---", output);
		Assert.Contains("second pass stderr", output);
	}

	// ============================================================
	//  Round 2 tests: title escape + metadata keywords + runtime
	// ============================================================

	private static (string tmpDir, PdfBuilder builder) CreateBuilderFixture(
		string configJson
	) {
		var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmpDir);
		var sourceDir = Directory.CreateDirectory(Path.Combine(tmpDir, "src"));
		File.WriteAllText(Path.Combine(sourceDir.FullName, "a.cpp"), "int main() { return 0; }");
		var outputPdf = new FileInfo(Path.Combine(tmpDir, "out.pdf"));
		var configFile = new FileInfo(Path.Combine(tmpDir, "cfg.json"));
		File.WriteAllText(configFile.FullName, configJson);

		var logger = new TestLogger();
		var texParser = new ConfigParser("TEX", logger, ConfigStrictness.Strict);
		var programParser = new ConfigParser("PROGRAM", logger, ConfigStrictness.Strict);
		texParser.ParseConfigFile(File.ReadAllText(configFile.FullName), configFile.FullName);
		programParser.ParseConfigFile(File.ReadAllText(configFile.FullName), configFile.FullName);

		var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);
		var resMgr = new ManifestResourceManager(logger);
		var builder = new PdfBuilder(logger, options, texParser, programParser, resMgr);
		return (tmpDir, builder);
	}

	[Fact]
	public void GenerateTexContent_TitleContentWithUnderscore_EscapesByDefault() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "title": { "content": "Algorithm_Reference" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"Algorithm\_Reference", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_AuthorWithPercent_EscapesByDefault() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "author": "John Doe, 50% off" } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"50\%", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_EscapeDisabled_PassesThroughUnderscore() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "title": { "content": "raw_underscore", "escape_latex_specials": false } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains("raw_underscore", tex);
			Assert.DoesNotContain(@"raw\_underscore", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_KeywordsArray_JoinedWithCommaSpace() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "metadata": { "keywords": ["alpha", "beta", "gamma"] } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"pdfkeywords={alpha, beta, gamma}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_DefaultOutput_NoUnresolvedPlaceholders() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.DoesNotContain("<<MINTED_OUTPUTDIR>>", tex);
			Assert.DoesNotContain("<<METADATA_KEYWORDS>>", tex);
			Assert.DoesNotContain("<<CONTENT>>", tex);
			Assert.DoesNotContain("##", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	private static string CreateTempDir() =>
		Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
		).FullName;
}
