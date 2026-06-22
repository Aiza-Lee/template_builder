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
			Assert.DoesNotContain("<<DOC_CLASS_COLUMNS>>", tex);
			Assert.DoesNotContain("<<LAYOUT_TOC_OPENING>>", tex);
			Assert.DoesNotContain("<<LAYOUT_BODY_OPENING>>", tex);
			Assert.DoesNotContain("##", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_LayoutColumns1_OmitsTwocolumnTokens() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "layout": { "columns": 1 } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[10pt,landscape,]{ctexart}", tex);
			// columns=1 + toc_in_columns 默认 false：DOC_CLASS 空、TOC/BODY opening 都空
			Assert.DoesNotContain(@"\twocolumn", tex.Substring(tex.IndexOf(@"\begin{document}", StringComparison.Ordinal)));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_LayoutColumns2TocInColumnsFalse_EmitsTwocolumnBeforeToc() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "layout": { "columns": 2, "toc_in_columns": false } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
			// body region（\begin{document} 之后）应该有 1 个 \onecolumn（title）+ 1 个 \twocolumn（TOC 前）
			var body = BodyRegion(tex);
			var oneCount = CountOccurrences(body, @"\onecolumn");
			var twoCount = CountOccurrences(body, @"\twocolumn");
			Assert.Equal(1, oneCount);
			Assert.Equal(1, twoCount);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_LayoutColumns2TocInColumnsTrue_EmitsOnecolumnBeforeTocAndTwocolumnBeforeBody() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "layout": { "columns": 2, "toc_in_columns": true } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
			// body region：2 个 \onecolumn（title + TOC 前）+ 1 个 \twocolumn（body 前）
			var body = BodyRegion(tex);
			Assert.Equal(2, CountOccurrences(body, @"\onecolumn"));
			Assert.Equal(1, CountOccurrences(body, @"\twocolumn"));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_LayoutColumns1_NoColumnTogglesInBody() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "layout": { "columns": 1 } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[10pt,landscape,]{ctexart}", tex);
			var body = BodyRegion(tex);
			// columns=1: body 区域只有 1 个 \onecolumn（title），没有 \twocolumn
			Assert.Equal(1, CountOccurrences(body, @"\onecolumn"));
			Assert.Equal(0, CountOccurrences(body, @"\twocolumn"));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	private static string BodyRegion(string tex) {
		var start = tex.IndexOf(@"\begin{document}", StringComparison.Ordinal);
		return start >= 0 ? tex.Substring(start) : tex;
	}

	private static int CountOccurrences(string haystack, string needle) {
		int count = 0, idx = 0;
		while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) {
			count++;
			idx += needle.Length;
		}
		return count;
	}

	[Fact]
	public void GenerateTexContent_FancyConfigSet_EmitsFancyheadTokens() {
		var (tmpDir, builder) = CreateBuilderFixture("""
			{
				"TEX": {
					"fancy": {
						"head_right": "Page \\thepage",
						"foot_center": "footer text"
					}
				}
			}
			""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\fancyhf{}", tex);
			Assert.Contains(@"\fancyhead[L]{}", tex);
			Assert.Contains(@"\fancyfoot[C]{footer text}", tex);
			Assert.Contains(@"\fancyhead[R]{Page \thepage}", tex);
			Assert.Contains(@"\headrulewidth", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_FancyConfigEmpty_EmitsEmptyFancyheadArgs() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			// 默认 config 下 fancy 块全空，输出 \fancyhead[L]{} 形式（合法 LaTeX）
			Assert.Contains(@"\fancyhead[L]{}", tex);
			Assert.Contains(@"\fancyhead[C]{}", tex);
			Assert.Contains(@"\fancyhead[R]{}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_SectionFormatSet_EmitsTitleformat() {
		var (tmpDir, builder) = CreateBuilderFixture("""
			{
				"TEX": {
					"section": {
						"format_section": "\\Large\\bfseries\\color{blue}"
					}
				}
			}
			""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\titleformat{\section}{\Large\bfseries\color{blue}}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_SectionFormatEmpty_EmitsEmptyTitleformatArg() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\titleformat{\section}{}", tex);
			Assert.Contains(@"\titleformat{\subsection}{}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	private static string CreateTempDir() =>
		Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
		).FullName;

	// ============================================================
	//  Round 3a tests: CJK font block (BoldFont/ItalicFont/AutoFake)
	// ============================================================

	[Fact]
	public void GenerateTexContent_CjkDefaults_EmitsAutoFakeTrueWithoutBoldItalic() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\setCJKmainfont{SimSun}[", tex);
			Assert.Contains(@"AutoFakeBold=true", tex);
			Assert.Contains(@"AutoFakeSlant=true", tex);
			Assert.DoesNotContain(@"BoldFont=", tex);
			Assert.DoesNotContain(@"ItalicFont=", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkBoldFontSet_EmitsBoldFontOption() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "global": { "cjk_main_bold_font": "SimHei Bold" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"BoldFont=SimHei Bold", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkItalicFontSet_EmitsItalicFontOption() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "global": { "cjk_main_italic_font": "KaiTi Italic" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"ItalicFont=KaiTi Italic", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkAutoFakeBoldFalse_EmitsAutoFakeBoldFalse() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "global": { "cjk_auto_fake_bold": false } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"AutoFakeBold=false", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkAutoFakeSlantFalse_EmitsAutoFakeSlantFalse() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "global": { "cjk_auto_fake_slant": false } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"AutoFakeSlant=false", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkEmptyBoldFont_OmitsBoldFontOption() {
		// cjk_main_bold_font 默认 "" → 不应输出 BoldFont=, 行
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.DoesNotContain(@"BoldFont=", tex);
			Assert.DoesNotContain(@"ItalicFont=", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkAllOptionsSet_EmitsAllFourLines() {
		var (tmpDir, builder) = CreateBuilderFixture("""
			{
				"TEX": {
					"global": {
						"cjk_main_font": "SimSun",
						"cjk_main_bold_font": "SimHei",
						"cjk_main_italic_font": "KaiTi",
						"cjk_auto_fake_bold": false,
						"cjk_auto_fake_slant": false
					}
				}
			}
			""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\setCJKmainfont{SimSun}[", tex);
			Assert.Contains(@"BoldFont=SimHei", tex);
			Assert.Contains(@"ItalicFont=KaiTi", tex);
			Assert.Contains(@"AutoFakeBold=false", tex);
			Assert.Contains(@"AutoFakeSlant=false", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_DefaultCjkSansFont_IsSimHei() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\setsansfont{SimHei}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_CjkSansFontSet_EmitsUserValue() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "global": { "cjk_sans_font": "Microsoft YaHei" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\setsansfont{Microsoft YaHei}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	// ============================================================
	//  Round 3a tests: docclass orientation + base font size
	// ============================================================

	[Fact]
	public void GenerateTexContent_DocclassDefaults_EmitsLandscape10pt() {
		var (tmpDir, builder) = CreateBuilderFixture("{}");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			// 默认 columns=2 → docclass 末尾带 twocolumn
			Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_DocclassPortrait_EmitsPortraitInBothPlaces() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "docclass": { "orientation": "portrait" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[10pt,portrait,twocolumn]{ctexart}", tex);
			// \geometry 块中的 orientation 占位符应同步替换
			Assert.Contains(@"portrait,", tex);
			Assert.DoesNotContain(@"landscape,", tex.Substring(tex.IndexOf(@"\geometry", StringComparison.Ordinal)));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void GenerateTexContent_DocclassBaseFontSize_EmitsUserValue() {
		var (tmpDir, builder) = CreateBuilderFixture(
			"""{ "TEX": { "docclass": { "base_font_size": "12pt" } } }""");
		try {
			var tex = builder.GenerateTexContent_ForTest();
			Assert.Contains(@"\documentclass[12pt,landscape,twocolumn]{ctexart}", tex);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}
}
