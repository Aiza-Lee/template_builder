using System;
using System.IO;
using System.Text;
using Core;
using Utils;
using Xunit;
using template_builder.Tests.Fixtures;

namespace template_builder.Tests;

public class PdfBuilderTests {
    [Fact]
    public void Cleanup_RemovesAllAuxiliaryExtensionsAndMidOutput() {
        using var tmp = TempDir.Create();
        var baseName = "book";

        // 模拟 xelatex 编译留下的中间文件
        var auxExts = new[] { ".aux", ".log", ".toc", ".out", ".nav", ".snm" };
        foreach (var ext in auxExts) {
            File.WriteAllText(Path.Combine(tmp.Path, baseName + ext), "stub");
        }
        File.WriteAllText(Path.Combine(tmp.Path, "mid-output.tex"), "stub");
        // 一个无关文件，断言它不会被误删
        var keepMe = Path.Combine(tmp.Path, "book.pdf");
        File.WriteAllText(keepMe, "%PDF-1.4 stub");

        var logger = new TestLogger();
        PdfBuilder.Cleanup(tmp.Path, baseName, logger);

        foreach (var ext in auxExts) {
            Assert.False(
                File.Exists(Path.Combine(tmp.Path, baseName + ext)),
                $"Expected {baseName}{ext} to be deleted");
        }
        Assert.False(File.Exists(Path.Combine(tmp.Path, "mid-output.tex")));
        Assert.True(File.Exists(keepMe), "book.pdf should NOT be deleted");
    }

    [Fact]
    public void Cleanup_HandlesMissingFilesGracefully() {
        using var tmp = TempDir.Create();
        var logger = new TestLogger();
        // 目录里什么也不创建，直接调用 Cleanup 不应抛异常
        var ex = Record.Exception(() => PdfBuilder.Cleanup(tmp.Path, "book", logger));
        Assert.Null(ex);
    }

    [Fact]
    public void SaveTexFile_WritesContentAndReturnsPath() {
        using var tmp = TempDir.Create();
        const string content = "\\section{Hello}\nSome TeX body";

        var path = PdfBuilder.SaveTexFile(content, tmp.Path);

        Assert.Equal(Path.Combine(tmp.Path, "mid-output.tex"), path);
        Assert.Equal(content, File.ReadAllText(path));
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

    /// <summary>
    /// Round 3d 整合：单一基础 fixture 方法。`fakeRunner` 为 null 时构造 PdfBuilder 用真实的 XelatexRunner
    ///（依赖本机 xelatex）；传入则用 FakeXelatexRunner 替换。
    /// 两个原 wrapper（CreateBuilderFixture / CreateBuilderFixtureWithFakeRunner）保留以避免改 50+ 调用点。
    /// </summary>
    private static (TempDir tmp, PdfBuilder builder, FakeXelatexRunner? runner) CreateFixtureCore(
        string configJson,
        FakeXelatexRunner? fakeRunner,
        TestLogger? customLogger = null
    ) {
        var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(sourceDir.FullName, "a.cpp"), "int main() { return 0; }");
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(configFile.FullName, configJson);

        var logger = customLogger ?? new TestLogger();
        var texParser = new ConfigParser("TEX", logger, ConfigStrictness.Strict);
        var programParser = new ConfigParser("PROGRAM", logger, ConfigStrictness.Strict);
        texParser.ParseConfigFile(File.ReadAllText(configFile.FullName), configFile.FullName);
        programParser.ParseConfigFile(File.ReadAllText(configFile.FullName), configFile.FullName);

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);
        var resMgr = new ManifestResourceManager();
        var builder = new PdfBuilder(logger, options, texParser, programParser, resMgr, fakeRunner);
        return (tmp, builder, fakeRunner);
    }

    /// <summary>
    /// 保留旧 signature：返回 (tmp, builder)，不暴露 fake runner。多数 GenerateTexContent_ForTest 类测试用它。
    /// </summary>
    private static (TempDir tmp, PdfBuilder builder) CreateBuilderFixture(
        string configJson
    ) {
        var (tmp, builder, _) = CreateFixtureCore(configJson, null);
        return (tmp, builder);
    }

    /// <summary>
    /// Round 3b 引入：注入 FakeXelatexRunner，返回 runner 实例供断言调用计数/参数。
    /// </summary>
    private static (TempDir tmp, PdfBuilder builder, FakeXelatexRunner runner) CreateBuilderFixtureWithFakeRunner(
        string configJson,
        TestLogger? customLogger = null
    ) {
        var fake = new FakeXelatexRunner();
        var (tmp, builder, runner) = CreateFixtureCore(configJson, fake, customLogger);
        return (tmp, builder, runner!);
    }

    [Fact]
    public void GenerateTexContent_TitleContentWithUnderscore_EscapesByDefault() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "content": "Algorithm_Reference" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"Algorithm\_Reference", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_AuthorWithPercent_EscapesByDefault() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "author": "John Doe, 50% off" } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"50\%", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_EscapeDisabled_PassesThroughUnderscore() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "content": "raw_underscore", "escape_latex_specials": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains("raw_underscore", tex);
            Assert.DoesNotContain(@"raw\_underscore", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_KeywordsArray_JoinedWithCommaSpace() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "metadata": { "keywords": ["alpha", "beta", "gamma"] } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"pdfkeywords={alpha, beta, gamma}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DefaultOutput_NoUnresolvedPlaceholders() {
        var (tmp, builder) = CreateBuilderFixture("{}");
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
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_LayoutColumns1_OmitsTwocolumnTokens() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "layout": { "columns": 1 } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,]{ctexart}", tex);
            // columns=1 + toc_in_columns 默认 false：DOC_CLASS 空、TOC/BODY opening 都空
            Assert.DoesNotContain(@"\twocolumn", tex.Substring(tex.IndexOf(@"\begin{document}", StringComparison.Ordinal)));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_LayoutColumns2TocInColumnsFalse_DefaultNoCover_NoRedundantToggles() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "layout": { "columns": 2, "toc_in_columns": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
            // 默认无独立封面页：documentclass 已经为 twocolumn，无需 \onecolumn 和 \twocolumn 冗余切换，无空白页
            var body = BodyRegion(tex);
            Assert.Equal(0, CountOccurrences(body, @"\onecolumn"));
            Assert.Equal(0, CountOccurrences(body, @"\twocolumn"));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_LayoutColumns2TocInColumnsFalse_WithCover_EmitsOnecolumnThenTwocolumn() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "enable_cover": true }, "layout": { "columns": 2, "toc_in_columns": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
            // 启用封面页：封面 1 个 \onecolumn + TOC 前恢复 1 个 \twocolumn
            var body = BodyRegion(tex);
            Assert.Equal(1, CountOccurrences(body, @"\onecolumn"));
            Assert.Equal(1, CountOccurrences(body, @"\twocolumn"));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_LayoutColumns2TocInColumnsTrue_EmitsOnecolumnBeforeTocAndTwocolumnBeforeBody() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "layout": { "columns": 2, "toc_in_columns": true } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
            // 独立单栏目录：TOC 前切 1 个 \onecolumn + 正文前切 1 个 \twocolumn
            var body = BodyRegion(tex);
            Assert.Equal(1, CountOccurrences(body, @"\onecolumn"));
            Assert.Equal(1, CountOccurrences(body, @"\twocolumn"));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_LayoutColumns1_NoColumnTogglesInBody() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "layout": { "columns": 1 } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,]{ctexart}", tex);
            var body = BodyRegion(tex);
            // columns=1 且默认无封面：全程单栏，body 区域无 \onecolumn 也无 \twocolumn
            Assert.Equal(0, CountOccurrences(body, @"\onecolumn"));
            Assert.Equal(0, CountOccurrences(body, @"\twocolumn"));
        } finally {
            tmp.Dispose();
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
        var (tmp, builder) = CreateBuilderFixture("""
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
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_FancyConfigEmpty_EmitsEmptyFancyheadArgs() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 默认 config 下 fancy 块全空，输出 \fancyhead[L]{} 形式（合法 LaTeX）
            Assert.Contains(@"\fancyhead[L]{}", tex);
            Assert.Contains(@"\fancyhead[C]{}", tex);
            Assert.Contains(@"\fancyhead[R]{}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_SectionFormatSet_EmitsTitleformat() {
        var (tmp, builder) = CreateBuilderFixture("""
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
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_SectionFormatEmpty_EmitsEmptyTitleformatArg() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\titleformat{\section}{}", tex);
            Assert.Contains(@"\titleformat{\subsection}{}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3a tests: CJK font block (BoldFont/ItalicFont/AutoFake)
    // ============================================================

    [Fact]
    public void GenerateTexContent_CjkDefaults_EmitsAutoFakeTrueWithoutBoldItalic() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\setCJKmainfont{SimSun}[", tex);
            Assert.Contains(@"AutoFakeBold=true", tex);
            Assert.Contains(@"AutoFakeSlant=true", tex);
            Assert.DoesNotContain(@"BoldFont=", tex);
            Assert.DoesNotContain(@"ItalicFont=", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkBoldFontSet_EmitsBoldFontOption() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_main_bold_font": "SimHei Bold" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"BoldFont=SimHei Bold", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkItalicFontSet_EmitsItalicFontOption() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_main_italic_font": "KaiTi Italic" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"ItalicFont=KaiTi Italic", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkAutoFakeBoldFalse_EmitsAutoFakeBoldFalse() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_auto_fake_bold": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"AutoFakeBold=false", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkAutoFakeSlantFalse_EmitsAutoFakeSlantFalse() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_auto_fake_slant": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"AutoFakeSlant=false", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkEmptyBoldFont_OmitsBoldFontOption() {
        // cjk_main_bold_font 默认 "" → 不应输出 BoldFont=, 行
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.DoesNotContain(@"BoldFont=", tex);
            Assert.DoesNotContain(@"ItalicFont=", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkAllOptionsSet_EmitsAllFourLines() {
        var (tmp, builder) = CreateBuilderFixture("""
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
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DefaultCjkSansFont_IsSimHei() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\setsansfont{SimHei}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkSansFontSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_sans_font": "Microsoft YaHei" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\setsansfont{Microsoft YaHei}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3a tests: docclass orientation + base font size
    // ============================================================

    [Fact]
    public void GenerateTexContent_DocclassDefaults_EmitsLandscape10pt() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 默认 columns=2 → docclass 末尾带 twocolumn
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DocclassPortrait_EmitsPortraitInBothPlaces() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "docclass": { "orientation": "portrait" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,portrait,twocolumn]{ctexart}", tex);
            // \geometry 块中的 orientation 占位符应同步替换
            Assert.Contains(@"portrait,", tex);
            Assert.DoesNotContain(@"landscape,", tex.Substring(tex.IndexOf(@"\geometry", StringComparison.Ordinal)));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DocclassBaseFontSize_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "docclass": { "base_font_size": "12pt" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[12pt,landscape,twocolumn]{ctexart}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3a tests: geometry headheight/headsep/footskip/columnrule
    // ============================================================

    [Fact]
    public void GenerateTexContent_GeometryDefaults_EmitsAllFourNewOptions() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"headheight=12pt", tex);
            Assert.Contains(@"headsep=20pt", tex);
            Assert.Contains(@"footskip=30pt", tex);
            Assert.Contains(@"bottom=1.2cm,", tex);
            Assert.DoesNotContain(@"\setlength{\columnseprule}", tex);
            Assert.DoesNotContain(@"columnrule=", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_GeometryHeadheightSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "geometry": { "headheight": "15pt" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"headheight=15pt", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_GeometryHeadsepSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "geometry": { "headsep": "25pt" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"headsep=25pt", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_GeometryFootskipSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "geometry": { "footskip": "40pt" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"footskip=40pt", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_GeometryColumnRuleTrue_EmitsColumnruleTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "geometry": { "column_rule": true } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\setlength{\columnseprule}{0.4pt}", tex);
            Assert.DoesNotContain(@"columnrule=", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3a tests: title font_size_cmd/alignment + toc title/dot_leaders
    // ============================================================

    [Fact]
    public void GenerateTexContent_TitleFontSizeDefault_EmitsLargeBfseries() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\title{\Large\bfseries", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitleFontSizeSet_EmitsUserCommand() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "font_size_cmd": "\\Huge\\itshape" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\title{\Huge\itshape", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitlePlaceholder_HasSpaceBetweenFontSizeCmdAndContent() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "content": "测试标题" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 验证 \Large\bfseries 与 中文标题之间存在空格分隔，防止 XeTeX 解析为未定义控制序列 \bfseries测试标题
            Assert.Contains(@"\title{\Large\bfseries 测试标题}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitleAlignmentFlushleft_EmitsFlushleftEnv() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "enable_cover": true, "note": "额外说明", "alignment": "flushleft" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\begin{flushleft}", tex);
            Assert.Contains(@"\end{flushleft}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitleAlignmentFlushright_EmitsFlushrightEnv() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "enable_cover": true, "note": "额外说明", "alignment": "flushright" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\begin{flushright}", tex);
            Assert.Contains(@"\end{flushright}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitleAlignmentDefault_IsCenter_WhenCoverEnabled() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "title": { "enable_cover": true, "note": "额外说明" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\begin{center}", tex);
            Assert.Contains(@"\end{center}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DefaultConfig_OmitsCoverPageAndAlignment() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 默认配置不输出独立封面页，避免占用首张纸面与意外空白页
            Assert.DoesNotContain(@"\maketitle", tex);
            Assert.DoesNotContain(@"\begin{center}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TocTitleDefault_IsMuLu() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\renewcommand{\contentsname}{目录}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TocTitleSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "toc": { "title": "Table of Contents" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\renewcommand{\contentsname}{Table of Contents}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TocDotLeadersDefault_OmitsDotsepDef() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 默认 dot_leaders=true → LaTeX 自然有点引导 → 不输出 \def\@dotsep{10000}
            Assert.DoesNotContain(@"\@dotsep", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TocDotLeadersFalse_EmitsDotsepSuppress() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "toc": { "dot_leaders": false } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\def\@dotsep{10000}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3a tests: minted / hyperref / titlesec extra knobs
    // ============================================================

    [Fact]
    public void GenerateTexContent_MintedDefaults_EmitsAllNineNewOptions() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"mathescape=false", tex);
            Assert.DoesNotContain(@"escapeinside=", tex);
            Assert.DoesNotContain(@"xleftmargin=", tex);
            Assert.DoesNotContain(@"xrightmargin=", tex);
            Assert.Contains(@"firstnumber=auto", tex);
            Assert.Contains(@"stepnumber=1", tex);
            Assert.DoesNotContain(@"numberstyle=", tex);
            Assert.Contains(@"showspaces=false", tex);
            Assert.Contains(@"showtabs=false", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedMathescapeTrue_EmitsMathescapeTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "mathescape": true } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"mathescape=true", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedEscapeinsideSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "escapeinside": "||" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"escapeinside=||", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedFirstnumberSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "firstnumber": "42" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"firstnumber=42", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedStepnumberSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "stepnumber": "5" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"stepnumber=5", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedShowspacesTrue_EmitsShowspacesTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "showspaces": true } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"showspaces=true", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedShowtabsTrue_EmitsShowtabsTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "showtabs": true } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"showtabs=true", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedXLeftMarginSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "xleftmargin": "10pt" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"xleftmargin=10pt", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedNumberstyleSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "numberstyle": "\\tiny\\color{red}" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"numberstyle=\tiny\color{red}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_HyperrefDefaults_EmitsAllFourNewOptions() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"citecolor=green", tex);
            Assert.Contains(@"anchorcolor=black", tex);
            Assert.Contains(@"pdfborder={0 0 0}", tex);
            Assert.Contains(@"pdflang=zh-CN", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_HyperrefCiteColorSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "hyperref": { "cite_color": "red" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"citecolor=red", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_HyperrefAnchorColorSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "hyperref": { "anchor_color": "blue" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"anchorcolor=blue", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_HyperrefPdfBorderSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "hyperref": { "pdf_border": "{1 1 1}" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"pdfborder={1 1 1}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_HyperrefPdfLangSet_EmitsUserValue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "hyperref": { "pdf_lang": "en-US" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"pdflang=en-US", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitlesecSeparatorDefault_Is1em() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\titleformat{\section}{}{\thesection}{1em}{}", tex);
            Assert.Contains(@"\titleformat{\subsection}{}{\thesubsection}{1em}{}", tex);
            Assert.Contains(@"\titleformat{\subsubsection}{}{\thesubsubsection}{1em}{}", tex);
            Assert.Contains(@"\titleformat{\paragraph}{}{\theparagraph}{1em}{}", tex);
            Assert.Contains(@"\titleformat{\subparagraph}{}{\thesubparagraph}{1em}{}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_TitlesecSeparatorSet_AppliesToAllLevels() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "section": { "format_separator": "2em" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\titleformat{\section}{}{\thesection}{2em}{}", tex);
            Assert.Contains(@"\titleformat{\subsection}{}{\thesubsection}{2em}{}", tex);
            Assert.Contains(@"\titleformat{\subsubsection}{}{\thesubsubsection}{2em}{}", tex);
            Assert.Contains(@"\titleformat{\paragraph}{}{\theparagraph}{2em}{}", tex);
            Assert.Contains(@"\titleformat{\subparagraph}{}{\thesubparagraph}{2em}{}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3b tests: timeout (via FakeXelatexRunner)
    // ============================================================

    [Fact]
    public void Build_TimeoutSecondsDefault_Is120() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        try {
            builder.Build();
            Assert.NotEmpty(runner.Calls);
            Assert.All(runner.Calls, c => Assert.Equal(120, c.TimeoutSeconds));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_TimeoutFromConfig_PassedToRunner() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "timeout_seconds": 30 } } }""");
        try {
            builder.Build();
            Assert.All(runner.Calls, c => Assert.Equal(30, c.TimeoutSeconds));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_TimeoutZero_PassedAsZero() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "timeout_seconds": 0 } } }""");
        try {
            builder.Build();
            Assert.All(runner.Calls, c => Assert.Equal(0, c.TimeoutSeconds));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_TimeoutClampedToMax600() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "timeout_seconds": 9999 } } }""");
        try {
            builder.Build();
            Assert.All(runner.Calls, c => Assert.Equal(600, c.TimeoutSeconds));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_TimeoutNegative_ClampedToZero() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "timeout_seconds": -5 } } }""");
        try {
            builder.Build();
            Assert.All(runner.Calls, c => Assert.Equal(0, c.TimeoutSeconds));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_RunnerTimedOut_ReturnsXelatexFailure() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        runner.Results.Enqueue(new XelatexResult(-1, "killed: timeout", true));
        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_RunnerExitCode1_StillReturnsXelatexFailure() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        runner.Results.Enqueue(new XelatexResult(1, "some error", false));
        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_RunnerStartFailed_EmitsTroubleshootingGuidance() {
        var logger = new TestLogger();
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}", logger);
        runner.Results.Enqueue(new XelatexResult(-1, "[failed to start: No such file or directory]\n", false));
        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("排查指引"));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void BuildXelatexArguments_EmitsExpectedFlags() {
        var midTex = new FileInfo(Path.Combine(Path.GetTempPath(), "jobname.tex"));
        var args = PdfBuilder.BuildXelatexArguments(midTex);
        Assert.Contains("-shell-escape", args);
        Assert.Contains("-interaction=nonstopmode", args);
        Assert.Contains("-jobname=jobname", args);
        Assert.Contains("\"" + midTex.FullName + "\"", args);
    }

    [Theory]
    [InlineData(null, "-shell-escape")]
    [InlineData("", "-shell-escape")]
    [InlineData("restricted", "-shell-restricted")]
    [InlineData("-shell-restricted", "-shell-restricted")]
    [InlineData("none", "-no-shell-escape")]
    [InlineData("false", "-no-shell-escape")]
    [InlineData("-no-shell-escape", "-no-shell-escape")]
    public void ResolveShellEscapeFlag_ResolvesCorrectMode(string? input, string expected) {
        Assert.Equal(expected, PdfBuilder.ResolveShellEscapeFlag(input));
    }

    [Fact]
    public void BuildXelatexArguments_WithRestrictedMode_EmitsRestrictedFlag() {
        var midTex = new FileInfo(Path.Combine(Path.GetTempPath(), "jobname.tex"));
        var args = PdfBuilder.BuildXelatexArguments(midTex, "-shell-restricted");
        Assert.Contains("-shell-restricted", args);
        Assert.DoesNotContain("-shell-escape ", args);
    }

    // ============================================================
    //  Round 3b tests: pass_count
    // ============================================================

    [Fact]
    public void Build_PassCountDefault_IsTwo() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        try {
            builder.Build();
            Assert.Equal(2, runner.Calls.Count);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_PassCount1_InvokesXelatexOnce() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "pass_count": 1 } } }""");
        try {
            builder.Build();
            Assert.Single(runner.Calls);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_PassCount0_ClampedToOne() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "pass_count": 0 } } }""");
        try {
            builder.Build();
            Assert.Single(runner.Calls);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_PassCount99_ClampedToFive() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner(
            """{ "PROGRAM": { "build": { "pass_count": 99 } } }""");
        try {
            builder.Build();
            Assert.Equal(5, runner.Calls.Count);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Round 3c tests: microtype + parskip
    // ============================================================

    [Fact]
    public void GenerateTexContent_MicrotypeDefaults_ProtrusionTrueOthersFalse() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage[protrusion=true,expansion=false,kerning=false]{microtype}", tex);
            Assert.Contains(@"\let\MT@setup@expansion\relax", tex);
            Assert.Contains(@"\let\old@PackageError\PackageError", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MicrotypeProtrusionDisabled_EmitsFalse() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "typesetting": { "microtype": { "protrusion": false } } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage[protrusion=false,expansion=false,kerning=false]{microtype}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MicrotypeExpansionEnabled_EmitsTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "typesetting": { "microtype": { "expansion": true } } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage[protrusion=true,expansion=true,kerning=false]{microtype}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MicrotypeKerningEnabled_EmitsTrue() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "typesetting": { "microtype": { "kerning": true } } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage[protrusion=true,expansion=false,kerning=true]{microtype}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_AllMicrotypeOptionsFalse_StillIncludesPackage() {
        var (tmp, builder) = CreateBuilderFixture("""
            {
                "TEX": {
                    "typesetting": {
                        "microtype": {
                            "protrusion": false,
                            "expansion": false,
                            "kerning": false
                        }
                    }
                }
            }
            """);
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage[protrusion=false,expansion=false,kerning=false]{microtype}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_ParskipDefault_OmitsPackage() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.DoesNotContain(@"\usepackage{parskip}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_ParskipEnabled_EmitsPackage() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "typesetting": { "parskip": { "enabled": true } } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\usepackage{parskip}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    // ============================================================
    //  Coverage 补充测试：覆盖之前漏掉的真实分支
    // ============================================================

    /// <summary>
    /// [UX-05] 修正编译成功判定：XeLaTeX 只要返回非零退出码（如 ExitCode 1），
    /// 即使工作目录中生成了临时 PDF 文件，也绝不判定为成功，必须返回 XelatexFailure (1) 并标记为 Failed；
    /// 临时 PDF 仅作为 DiagnosticPdfPath 保留供排查，绝不覆盖目标 PDF 产物。
    /// </summary>
    [Fact]
    public void Build_RunnerExitCode1PdfExists_FailsAndIsolatesDiagnosticPdf() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var outputPdfPath = Path.Combine(tmp.Path, "output.pdf");
        File.WriteAllText(outputPdfPath, "%PDF-1.4 ORIGINAL VALID CONTENT");

        // 模拟 xelatex 非零退出（如编译报错），但写入了半成品 mid-output.pdf
        runner.Results.Enqueue(new XelatexResult(1, "! Package minted Error: missing pygmentize\nl.42 \\begin{minted}", false, "! Package minted Error: missing pygmentize\nl.42 \\begin{minted}"));
        runner.SideEffects.Enqueue((workingDir, _) => {
            Directory.CreateDirectory(workingDir);
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 CORRUPTED STUB");
        });

        try {
            var exitCode = builder.Build();
            // 严禁非零退出伪装成功
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);

            // 原始输出文件必须完好无损，绝不被半成品覆盖
            var originalContent = File.ReadAllText(outputPdfPath);
            Assert.Equal("%PDF-1.4 ORIGINAL VALID CONTENT", originalContent);

            // 诊断产物被保留在构建目录
            Assert.NotNull(builder.LastBuildResult);
            Assert.Equal(Core.Models.BuildStatus.Failed, builder.LastBuildResult.Status);
            Assert.NotNull(builder.LastBuildResult.DiagnosticPdfPath);
            Assert.True(File.Exists(builder.LastBuildResult.DiagnosticPdfPath));

            // 只执行了 1 轮编译，出问题立即中止，不盲目继续后续 pass
            Assert.Single(runner.Calls);
        } finally {
            tmp.Dispose();
        }
    }

    /// <summary>
    /// [UX-05] 验证构建失败时提取的诊断结果包含定位到的源码文件与修复建议。
    /// </summary>
    [Fact]
    public void Build_RunnerExitCode1_PopulatesCulpritSourceAndRecommendation() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var codeFile = Path.Combine(tmp.Path, "src", "demo.py");
        File.WriteAllText(codeFile, "print('hello world')\n");

        runner.Results.Enqueue(new XelatexResult(
            1,
            "! Undefined control sequence.\nl.15 \\badcmd",
            false,
            "! Undefined control sequence.\nl.15 \\badcmd"
        ));

        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);

            var res = builder.LastBuildResult;
            Assert.NotNull(res);
            Assert.Equal(Core.Models.BuildStatus.Failed, res.Status);
            Assert.Contains("Undefined control sequence", res.FailureReason);
            Assert.NotNull(res.Recommendation);
            Assert.Contains("未定义", res.Recommendation);
        } finally {
            tmp.Dispose();
        }
    }

    /// <summary>
    /// 直接验证 TemplatePlaceholderScanner 检测未注册的 ##KEY## / <<KEY>> 占位符——
    /// 这条对应 PdfBuilder.Build 中 unresolved-placeholder 早返（exit 3）的输入端。
    /// </summary>
    [Fact]
    public void TemplatePlaceholderScanner_FindsUnregisteredKeyAndRuntimeToken() {
        var unresolved = TemplatePlaceholderScanner.FindUnresolved(
            "before ##NOT_REGISTERED## after <<MUST_FAIL>> end"
        ).ToList();
        Assert.Contains("##NOT_REGISTERED##", unresolved);
        Assert.Contains("<<MUST_FAIL>>", unresolved);
    }

    /// <summary>
    /// PdfBuilder.SaveTexFile 写出的内容应原样写盘——验证 helper 没有意外修改。
    /// 同时也覆盖了 internal static SaveTexFile 的写盘路径。
    /// </summary>
    [Fact]
    public void SaveTexFile_RoundtripsContentByteForByte() {
        using var tmp = TempDir.Create();
        const string content = "\\section{Hello}\n% comment\nbody\n";
        var written = PdfBuilder.SaveTexFile(content, tmp.Path);
        Assert.Equal(content, File.ReadAllText(written));
    }


    [Fact]
    public void BuildXelatexArguments_IncludesFileLineError() {
        var midTex = new FileInfo(Path.Combine(Path.GetTempPath(), "jobname.tex"));
        var args = PdfBuilder.BuildXelatexArguments(midTex);
        Assert.Contains("-file-line-error", args);
    }

    [Fact]
    public void GenerateTexContent_MintedOutputdirOverride_RedirectsMinted() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "code": { "minted_outputdir": "/tmp/tb-minted-cache" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"outputdir=/tmp/tb-minted-cache", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_MintedOutputdirEmpty_DefaultsToPdfDir() {
        // 空字符串（默认）→ minted outputdir 走 PDF 输出目录
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            var expectedDir = Path.Combine(tmp.Path, "out.pdf");
            // out.pdf 不存在，但 DirectoryName 可由 FileInfo 推得
            var expectedOutputDir = Path.GetDirectoryName(expectedDir)!.Replace("\\", "/");
            Assert.Contains($"outputdir={expectedOutputDir}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_RegexReplaces_ProducesIdenticalOutput() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // 不应残留任何未替换的 ##KEY## 占位符（除了 value 自身就是 ##OTHER## 这种极小概率）
            Assert.DoesNotContain("##GLOBAL_MAIN_FONT##", tex);
            Assert.DoesNotContain("##DOC_PAPER_SIZE##", tex);
            // 默认 config 已知会注入的若干 token
            Assert.Contains(@"\setmainfont{Times New Roman}", tex);
            Assert.Contains(@"a4paper", tex);
        } finally {
            tmp.Dispose();
        }
    }

    /// <summary>
    /// 兼容测试：原循环实现的"value 含 ##OTHER## 时被递归替换"语义。
    /// 把 GLOBAL_MAIN_FONT 设为 ##AUTHOR##，最终输出应为 AUTHOR 的值（默认 "Aiza"）。
    /// </summary>
    [Fact]
    public void ReplaceMainPlaceholders_ValueContainsAnotherPlaceholder_ExpandsBoth() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "author": "CustomAuthor", "global": { "main_font": "##AUTHOR##" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            // ##AUTHOR## 已被 AUTHOR 的值（"CustomAuthor"）替换
            Assert.DoesNotContain("##AUTHOR##", tex);
            Assert.Contains(@"\setmainfont{CustomAuthor}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_PurgesBuildDirBeforeBuilding_DoesNotProduceBuildSection() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var buildDir = Path.Combine(tmp.Path, "src", "build");
        Directory.CreateDirectory(buildDir);
        File.WriteAllText(Path.Combine(buildDir, "old.aux"), "old aux content");
        File.WriteAllText(Path.Combine(buildDir, "mid-output.pdf"), "old pdf");
        string? generatedTex = null;
        runner.Results.Enqueue(new XelatexResult(0, "", false));
        runner.SideEffects.Enqueue((workingDir, _) => {
            generatedTex = File.ReadAllText(Path.Combine(workingDir, "mid-output.tex"));
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 stub");
        });

        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.NotNull(generatedTex);
            Assert.DoesNotContain(@"\section{build}", generatedTex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_XelatexRunsInBuildDirectory_NotBaseDirectory() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var expectedBuildDir = Path.Combine(tmp.Path, "src", ".template_builder", "build");
        try {
            builder.Build();
            Assert.NotEmpty(runner.Calls);
            Assert.Equal(expectedBuildDir, runner.Calls[0].WorkingDir);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Cleanup_RemovesAexAndW18Files() {
        using var tmp = TempDir.Create();
        var aex = Path.Combine(tmp.Path, "mid-output.aex");
        var w18 = Path.Combine(tmp.Path, "mid-output.w18");
        File.WriteAllText(aex, "minted test");
        File.WriteAllText(w18, "minted w18 test");
        Assert.True(File.Exists(aex));
        Assert.True(File.Exists(w18));

        PdfBuilder.Cleanup(tmp.Path, "mid-output", new TestLogger());

        Assert.False(File.Exists(aex));
        Assert.False(File.Exists(w18));
    }

    [Fact]
    public void GenerateTexContent_DocclassFontset_EmitsFontsetOption() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "docclass": { "fontset": "ubuntu" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn,fontset=ubuntu]{ctexart}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_DocclassFontsetAuto_OmitsFontsetOption() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "docclass": { "fontset": "auto" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\documentclass[10pt,landscape,twocolumn]{ctexart}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkMainFontAuto_OmitsSetCJKmainfont() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_main_font": "auto" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.DoesNotContain(@"\setCJKmainfont", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkFallbackChain_ContainsSimSunNotoAndFandol() {
        var (tmp, builder) = CreateBuilderFixture("{}");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\IfFontExistsTF{SimSun}", tex);
            Assert.Contains(@"\IfFontExistsTF{Noto Serif CJK SC}", tex);
            Assert.Contains(@"\IfFontExistsTF{FandolSong}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void GenerateTexContent_CjkCustomFont_PrependsToFallbackChain() {
        var (tmp, builder) = CreateBuilderFixture(
            """{ "TEX": { "global": { "cjk_main_font": "Source Han Serif SC" } } }""");
        try {
            var tex = builder.GenerateTexContent_ForTest();
            Assert.Contains(@"\IfFontExistsTF{Source Han Serif SC}", tex);
            Assert.Contains(@"\IfFontExistsTF{SimSun}", tex);
            Assert.Contains(@"\IfFontExistsTF{Noto Serif CJK SC}", tex);
            Assert.Contains(@"\IfFontExistsTF{FandolSong}", tex);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_DiscoversSourceFontsDir_AndPassesToRunner() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var fontsDir = Path.Combine(tmp.Path, "src", "fonts");
        Directory.CreateDirectory(fontsDir);
        try {
            builder.Build();
            Assert.NotEmpty(runner.Calls);
            Assert.NotNull(runner.Calls[0].ExtraFontDirs);
            Assert.Contains(fontsDir, runner.Calls[0].ExtraFontDirs!);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_CustomFontDirs_ResolvedAndPassedToRunner() {
        using var extraDir = TempDir.Create();
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner($$"""
            {
                "TEX": {
                    "global": {
                        "custom_font_dirs": ["{{extraDir.Path.Replace("\\", "/")}}"]
                    }
                }
            }
            """);
        try {
            builder.Build();
            Assert.NotEmpty(runner.Calls);
            Assert.NotNull(runner.Calls[0].ExtraFontDirs);
            Assert.Contains(extraDir.Path, runner.Calls[0].ExtraFontDirs!);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_UserFilesInBuildDir_PreservedOnSuccess() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var userBuildDir = Path.Combine(tmp.Path, "src", "build");
        Directory.CreateDirectory(userBuildDir);
        var userFile = Path.Combine(userBuildDir, "user_script.sh");
        File.WriteAllText(userFile, "#!/bin/bash\necho hello");

        runner.Results.Enqueue(new XelatexResult(0, "", false));
        runner.SideEffects.Enqueue((workingDir, _) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 valid");
        });

        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.True(File.Exists(userFile), "源目录 build/ 下的用户文件在成功构建后必须被保留");
            Assert.Equal("#!/bin/bash\necho hello", File.ReadAllText(userFile));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_UserFilesInBuildDir_PreservedOnFailure() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var userBuildDir = Path.Combine(tmp.Path, "src", "build");
        Directory.CreateDirectory(userBuildDir);
        var userFile = Path.Combine(userBuildDir, "user_data.bin");
        File.WriteAllText(userFile, "precious user data");

        // 模拟编译失败
        runner.Results.Enqueue(new XelatexResult(1, "! Fatal error", false));

        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);
            Assert.True(File.Exists(userFile), "源目录 build/ 下的用户文件在编译失败时必须被保留");
            Assert.Equal("precious user data", File.ReadAllText(userFile));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_CompilationFailure_PreservesExistingOutputPdf() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        var outPdfPath = Path.Combine(tmp.Path, "out.pdf");
        const string originalPdfContent = "%PDF-1.4 previous good version";
        File.WriteAllText(outPdfPath, originalPdfContent);

        // 模拟编译失败
        runner.Results.Enqueue(new XelatexResult(1, "error", false));

        try {
            var exitCode = builder.Build();
            Assert.Equal(ExitCodes.XelatexFailure, exitCode);
            Assert.True(File.Exists(outPdfPath));
            Assert.Equal(originalPdfContent, File.ReadAllText(outPdfPath));
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void Build_UnresolvedPlaceholders_PreservesExistingOutputPdf() {
        // 使用一个会导致未解析占位符的 templateDir
        using var templateDir = TempDir.Create();
        File.WriteAllText(Path.Combine(templateDir.Path, "Main.tex"), "Doc with <<NON_EXISTENT_PLACEHOLDER>>");

        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "test.cs"), "class A {}");
        var outPdf = Path.Combine(tmp.Path, "doc.pdf");
        const string previousPdf = "%PDF-1.4 intact";
        File.WriteAllText(outPdf, previousPdf);

        var texParser = new ConfigParser("TEX", new TestLogger(), ConfigStrictness.Lax);
        var progParser = new ConfigParser("PROGRAM", new TestLogger(), ConfigStrictness.Lax);
        var resMgr = new ManifestResourceManager();
        var runner = new FakeXelatexRunner();
        var dummyConfig = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(dummyConfig.FullName, "{}");
        var options = new BuildSubcommandOptions(
            new DirectoryInfo(srcDir),
            new FileInfo(outPdf),
            dummyConfig,
            Verbose: false,
            TemplateDir: new DirectoryInfo(templateDir.Path)
        );

        var builder = new PdfBuilder(new TestLogger(), options, texParser, progParser, resMgr, runner);
        var exitCode = builder.Build();

        Assert.Equal(ExitCodes.UnresolvedPlaceholders, exitCode);
        Assert.True(File.Exists(outPdf));
        Assert.Equal(previousPdf, File.ReadAllText(outPdf));
    }

    [Fact]
    public void ExecuteBuild_SuccessfulCompilation_ReturnsStructuredSuccess() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        try {
            // 配置两轮编译成功并生成模拟有效 PDF
            runner.Results.Enqueue(new XelatexResult(0, "", false));
            runner.SideEffects.Enqueue((workingDir, args) => {
                var pdfPath = Path.Combine(workingDir, "mid-output.pdf");
                File.WriteAllText(pdfPath, "%PDF-1.4 simulated pdf");
            });

            runner.Results.Enqueue(new XelatexResult(0, "", false));
            runner.SideEffects.Enqueue((workingDir, args) => {
                var pdfPath = Path.Combine(workingDir, "mid-output.pdf");
                File.WriteAllText(pdfPath, "%PDF-1.4 simulated pdf");
            });

            var result = builder.ExecuteBuild();

            Assert.True(result.IsSuccess);
            Assert.Equal(Core.Models.BuildStatus.Success, result.Status);
            Assert.Equal(ExitCodes.Success, result.ExitCode);
            Assert.NotNull(result.OutputPdf);
            Assert.True(result.OutputPdf.Exists);
            Assert.Equal(2, result.PassesCompleted);
            Assert.True(result.Duration > TimeSpan.Zero);
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void ExecuteBuild_ConstructedWithProjectConfiguration_RecordsConfigurationAndBuilds() {
        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "test.cs"), "class Program {}");
        var outPdf = Path.Combine(tmp.Path, "out.pdf");
        var cfgFile = Path.Combine(tmp.Path, "cfg.json");
        File.WriteAllText(cfgFile, "{}");

        var projectConfig = new Core.Models.ProjectConfiguration();
        projectConfig.TrySetOverride("LAYOUT_COLUMNS", "1");

        var options = new BuildSubcommandOptions(
            new DirectoryInfo(srcDir),
            new FileInfo(outPdf),
            new FileInfo(cfgFile),
            Verbose: false,
            TemplateDir: null
        );

        var runner = new FakeXelatexRunner();
        runner.SideEffects.Enqueue((workingDir, args) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 mock");
        });
        runner.SideEffects.Enqueue((workingDir, args) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 mock");
        });

        var builder = new PdfBuilder(new TestLogger(), options, projectConfig, new ManifestResourceManager(), runner);
        Assert.Same(projectConfig, builder.ProjectConfig);

        var result = builder.ExecuteBuild();
        Assert.True(result.IsSuccess);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public void Build_DoesNotTouchUserBuildDirOrUserFiles() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        try {
            var srcDir = Path.Combine(tmp.Path, "src");
            var userBuildDir = Directory.CreateDirectory(Path.Combine(srcDir, "build"));
            var userCodeFile = Path.Combine(userBuildDir.FullName, "mid-output.cpp");
            var userScriptFile = Path.Combine(userBuildDir.FullName, "mid-output.py");
            var userMintedDir = Directory.CreateDirectory(Path.Combine(userBuildDir.FullName, "_minted_user_data"));
            var userNote = Path.Combine(userMintedDir.FullName, "notes.txt");

            File.WriteAllText(userCodeFile, "// user cpp source");
            File.WriteAllText(userScriptFile, "# user python script");
            File.WriteAllText(userNote, "user notes");

            // 模拟构建运行
            builder.Build();

            // 验证用户目录与文件完整保留，未被 PurgeWorkDir 或清理流程触碰
            Assert.True(File.Exists(userCodeFile), "用户源文件 build/mid-output.cpp 不应被删除");
            Assert.True(File.Exists(userScriptFile), "用户脚本 build/mid-output.py 不应被删除");
            Assert.True(File.Exists(userNote), "用户目录中 notes.txt 不应被删除");
            Assert.True(Directory.Exists(userMintedDir.FullName), "用户自建 _minted_user_data 目录不应被删除");
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void PurgeWorkDir_DoesNotDeleteNonToolExtensionsInWorkDir() {
        var (tmp, builder, runner) = CreateBuilderFixtureWithFakeRunner("{}");
        try {
            var srcDir = Path.Combine(tmp.Path, "src");
            var toolWorkDir = Directory.CreateDirectory(Path.Combine(srcDir, ".template_builder", "build"));
            var userFileInWorkDir = Path.Combine(toolWorkDir.FullName, "mid-output.cpp");
            var userCustomDirInWorkDir = Directory.CreateDirectory(Path.Combine(toolWorkDir.FullName, "_minted_custom"));
            var auxFile = Path.Combine(toolWorkDir.FullName, "mid-output.aux");

            File.WriteAllText(userFileInWorkDir, "important code");
            File.WriteAllText(auxFile, "dummy aux");
            File.WriteAllText(Path.Combine(userCustomDirInWorkDir.FullName, "data.txt"), "data");

            builder.Build();

            // 工具的 mid-output.aux 会被清理，但用户非工具扩展名及自建目录必须受保护
            Assert.True(File.Exists(userFileInWorkDir), "工作目录中的非工具产物 mid-output.cpp 不应被清理");
            Assert.True(Directory.Exists(userCustomDirInWorkDir.FullName), "工作目录中的非专属 minted 目录不应被清理");
        } finally {
            tmp.Dispose();
        }
    }

    [Fact]
    public void IsExternalLinkOrSymlink_DetectsExternalDirectoryTarget() {
        using var tmp = TempDir.Create();
        var rootDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "root"));
        var insideDir = Directory.CreateDirectory(Path.Combine(rootDir.FullName, "inside"));
        var outsideDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "outside"));

        Assert.False(PdfBuilder.IsExternalLinkOrSymlink(insideDir.FullName, rootDir.FullName));

        // 创建指向外部的软链接
        var linkPath = Path.Combine(rootDir.FullName, "ext_link");
        try {
            Directory.CreateSymbolicLink(linkPath, outsideDir.FullName);
            Assert.True(PdfBuilder.IsExternalLinkOrSymlink(linkPath, rootDir.FullName));
        } catch {
            // 环境若不支持软链接创建则跳过软链接断言
        }
    }

    [Fact]
    public void Build_DirectoryPathContainingTex_CorrectlyFindsArtifactsAndSucceeds() {
        using var tmp = TempDir.Create();
        var complexSrcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "folder.tex.data", "sub.tex dir with space", "源码目录.tex"));
        File.WriteAllText(Path.Combine(complexSrcDir.FullName, "hello.cpp"), "int main() {}");

        var outDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "out.tex.dir", "中文 输出.tex"));
        var outPdf = new FileInfo(Path.Combine(outDir.FullName, "result.tex.spec.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, "{}");

        var options = new BuildSubcommandOptions(complexSrcDir, outPdf, configFile, Verbose: false, TemplateDir: null);
        var projectConfig = new Core.Models.ProjectConfiguration();
        var runner = new FakeXelatexRunner();

        runner.SideEffects.Enqueue((workingDir, args) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 mock pdf content");
            File.WriteAllText(Path.Combine(workingDir, "mid-output.log"), "mock xelatex log");
        });

        var builder = new PdfBuilder(new TestLogger(), options, projectConfig, new ManifestResourceManager(), runner);

        var result = builder.ExecuteBuild();

        Assert.True(result.IsSuccess);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(outPdf.Exists);
        Assert.StartsWith("%PDF-1.4", File.ReadAllText(outPdf.FullName));
    }

    [Fact]
    public void Build_DirectoryPathContainingTex_OnFailure_DiagnosticPdfAndLogCorrectlyLocated() {
        using var tmp = TempDir.Create();
        var complexSrcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "folder.tex.data", "sub.tex dir with space", "源码目录.tex"));
        File.WriteAllText(Path.Combine(complexSrcDir.FullName, "hello.cpp"), "int main() {}");

        var outDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "out.tex.dir", "中文 输出.tex"));
        var outPdf = new FileInfo(Path.Combine(outDir.FullName, "result.tex.spec.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, "{}");

        var options = new BuildSubcommandOptions(complexSrcDir, outPdf, configFile, Verbose: false, TemplateDir: null);
        var projectConfig = new Core.Models.ProjectConfiguration();
        var runner = new FakeXelatexRunner();

        runner.Results.Enqueue(new XelatexResult(1, "! Fatal error occurred", false));
        runner.SideEffects.Enqueue((workingDir, args) => {
            File.WriteAllText(Path.Combine(workingDir, "mid-output.pdf"), "%PDF-1.4 partial failure pdf");
            File.WriteAllText(Path.Combine(workingDir, "mid-output.log"), "! Fatal error occurred on line 1");
        });

        var builder = new PdfBuilder(new TestLogger(), options, projectConfig, new ManifestResourceManager(), runner);

        var result = builder.ExecuteBuild();

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.XelatexFailure, result.ExitCode);
        Assert.NotNull(result.DiagnosticPdfPath);
        Assert.Equal(Path.Combine(builder.GetWorkDirectory(), "mid-output.pdf"), result.DiagnosticPdfPath);
        Assert.NotNull(result.LogFilePath);
        Assert.Equal(Path.Combine(builder.GetWorkDirectory(), "mid-output.log"), result.LogFilePath);
    }
}

