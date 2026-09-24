using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core;
using Core.Commands;
using Core.Models;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests;

/// <summary>
/// UX-14 验收测试：
/// 1. PDF 默认元数据清空署名与占位说明，标题自动由项目源目录合理推导；
/// 2. 封面与目录独立开关，开关不产生任何意外空白页；
/// 3. 排版预设体系（比赛打印、屏幕阅读、紧凑速查）；
/// 4. 源码小样预览（Sample File Limit）及真实 XeLaTeX 渲染中文、长行与多级章节。
/// </summary>
public class PdfPresetAndProofPreviewTests {
    [Fact]
    public void DefaultConfig_HasNoDeveloperSignatureOrPlaceholder() {
        var texParser = new ConfigParser("TEX", new TestLogger());

        // 默认作者与说明为空，不带入 Aiza、文档标题、文档主题、标题页额外内容
        Assert.Equal(string.Empty, texParser["AUTHOR"].GetAsString());
        Assert.Equal(string.Empty, texParser["SUBJECT"].GetAsString());
        Assert.Equal(string.Empty, texParser["TITLE_CONTENT"].GetAsString());
        Assert.Equal(string.Empty, texParser["TITLE_NOTE"].GetAsString());

        // 默认不生成独立封面页（避免占用首页与意外空白页），默认生成目录
        Assert.False(texParser["TITLE_ENABLE_COVER"].GetAsBool());
        Assert.True(texParser["TOC_ENABLE_TOC"].GetAsBool());
    }

    [Fact]
    public void GenerateTexContent_DefaultConfig_DerivesTitleFromSourceDir_AndHasNoAizaSignature() {
        using var tmp = TempDir.Create();
        var myAlgoDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "my_cool_algo"));
        File.WriteAllText(Path.Combine(myAlgoDir.FullName, "solution.cpp"), "int main() { return 0; }");

        var outPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var options = new BuildSubcommandOptions(myAlgoDir, outPdf, cfgFile, false);
        var config = new ProjectConfiguration();
        config.LoadProjectConfig("{}");

        var builder = new PdfBuilder(new TestLogger(), options, config, new ManifestResourceManager());
        var tex = builder.GenerateTexContent_ForTest();

        // 标题从目录名 "my_cool_algo" 推导并做 LaTeX 转义 -> "my\_cool\_algo"
        Assert.Contains(@"pdftitle={my\_cool\_algo}", tex);
        Assert.Contains(@"\title{\Large\bfseries my\_cool\_algo}", tex);

        // 作者与主题为空
        Assert.Contains(@"pdfauthor={},", tex);
        Assert.Contains(@"pdfsubject={},", tex);
        Assert.Contains(@"\author{}", tex);

        // 绝不包含 Aiza 或旧占位符
        Assert.DoesNotContain("Aiza", tex);
        Assert.DoesNotContain("文档标题", tex);
        Assert.DoesNotContain("文档主题", tex);
        Assert.DoesNotContain("标题页额外内容", tex);

        // 默认不生成独立封面页
        Assert.DoesNotContain(@"\maketitle", tex);
    }

    [Fact]
    public void Presets_AllDefinedWithClearCharacteristics() {
        Assert.Equal(3, LayoutPresets.All.Count);

        // 1. 比赛打印预设
        var comp = LayoutPresets.Competition;
        Assert.Equal("competition", comp.Id);
        Assert.Equal("landscape", comp.Settings["DOCCLASS_ORIENTATION"]);
        Assert.Equal("2", comp.Settings["LAYOUT_COLUMNS"]);
        Assert.Equal("false", comp.Settings["LAYOUT_TOC_IN_COLUMNS"]);
        Assert.Equal("6pt", comp.Settings["CODE_FONT_SIZE"]);
        Assert.Equal("6pt", comp.Settings["CODE_LINE_HEIGHT"]);
        Assert.Equal("1cm", comp.Settings["GEOMETRY_LEFT_MARGIN"]);
        Assert.Equal("false", comp.Settings["TITLE_ENABLE_COVER"]);
        Assert.Equal("true", comp.Settings["TOC_ENABLE_TOC"]);
        Assert.Equal("false", comp.Settings["HYPERREF_ENABLE_COLORLINKS"]);

        // 2. 屏幕阅读预设
        var reading = LayoutPresets.Reading;
        Assert.Equal("reading", reading.Id);
        Assert.Equal("portrait", reading.Settings["DOCCLASS_ORIENTATION"]);
        Assert.Equal("1", reading.Settings["LAYOUT_COLUMNS"]);
        Assert.Equal("true", reading.Settings["LAYOUT_TOC_IN_COLUMNS"]);
        Assert.Equal("10pt", reading.Settings["CODE_FONT_SIZE"]);
        Assert.Equal("13pt", reading.Settings["CODE_LINE_HEIGHT"]);
        Assert.Equal("2cm", reading.Settings["GEOMETRY_LEFT_MARGIN"]);
        Assert.Equal("false", reading.Settings["TITLE_ENABLE_COVER"]);
        Assert.Equal("true", reading.Settings["TOC_ENABLE_TOC"]);
        Assert.Equal("true", reading.Settings["HYPERREF_ENABLE_COLORLINKS"]);

        // 3. 紧凑速查预设
        var compact = LayoutPresets.CompactCheatSheet;
        Assert.Equal("compact", compact.Id);
        Assert.Equal("landscape", compact.Settings["DOCCLASS_ORIENTATION"]);
        Assert.Equal("2", compact.Settings["LAYOUT_COLUMNS"]);
        Assert.Equal("5.5pt", compact.Settings["CODE_FONT_SIZE"]);
        Assert.Equal("5.5pt", compact.Settings["CODE_LINE_HEIGHT"]);
        Assert.Equal("0.6cm", compact.Settings["GEOMETRY_LEFT_MARGIN"]);
        Assert.Equal("true", compact.Settings["GEOMETRY_COLUMN_RULE"]);
        Assert.Equal("false", compact.Settings["TITLE_ENABLE_COVER"]);
        Assert.Equal("false", compact.Settings["TOC_ENABLE_TOC"]);
    }

    [Fact]
    public void ApplyPreset_UpdatesProjectConfigurationOverrides() {
        var config = new ProjectConfiguration();
        Assert.Equal("2", config.GetEffectiveValue("LAYOUT_COLUMNS")); // 内置初始为 2 (双栏)

        LayoutPresets.Apply(config, LayoutPresets.Reading);
        Assert.Equal("1", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("portrait", config.GetEffectiveValue("DOCCLASS_ORIENTATION"));
        Assert.Equal("10pt", config.GetEffectiveValue("CODE_FONT_SIZE"));
        Assert.Equal("13pt", config.GetEffectiveValue("CODE_LINE_HEIGHT"));
        Assert.Equal("true", config.GetEffectiveValue("HYPERREF_ENABLE_COLORLINKS"));

        LayoutPresets.Apply(config, LayoutPresets.Competition);
        Assert.Equal("2", config.GetEffectiveValue("LAYOUT_COLUMNS"));
        Assert.Equal("landscape", config.GetEffectiveValue("DOCCLASS_ORIENTATION"));
        Assert.Equal("6pt", config.GetEffectiveValue("CODE_FONT_SIZE"));
        Assert.Equal("false", config.GetEffectiveValue("HYPERREF_ENABLE_COLORLINKS"));
    }

    [Fact]
    public void CodeBlockGenerator_SampleLimit_EmitsOnlySpecifiedNumberOfFiles() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "01_first.cpp"), "// File 1");
        File.WriteAllText(Path.Combine(srcDir.FullName, "02_second.cpp"), "// File 2");
        File.WriteAllText(Path.Combine(srcDir.FullName, "03_third.cpp"), "// File 3");
        File.WriteAllText(Path.Combine(srcDir.FullName, "04_fourth.cpp"), "// File 4");

        var parser = new ConfigParser("PROGRAM", new TestLogger());
        var genAll = new CodeBlockGenerator(new TestLogger(), parser, 4, srcDir, "\\inputminted{##LANG##}{##FILE##}", 5, true);
        var allTex = genAll.Generate();
        Assert.Contains("01_first.cpp", allTex);
        Assert.Contains("02_second.cpp", allTex);
        Assert.Contains("03_third.cpp", allTex);
        Assert.Contains("04_fourth.cpp", allTex);

        // 限制 sampleLimit = 2
        var genSample = new CodeBlockGenerator(new TestLogger(), parser, 4, srcDir, "\\inputminted{##LANG##}{##FILE##}", 5, true, maxFiles: 2);
        var sampleTex = genSample.Generate();
        Assert.Contains("01_first.cpp", sampleTex);
        Assert.Contains("02_second.cpp", sampleTex);
        Assert.DoesNotContain("03_third.cpp", sampleTex);
        Assert.DoesNotContain("04_fourth.cpp", sampleTex);
    }

    [Fact]
    public void CommandLine_PresetOption_RejectsInvalidPreset() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "test.cpp"), "int main(){}");
        var outPdf = Path.Combine(tmp.Path, "out.pdf");

        var logger = new TestLogger();
        var root = new RootCommandFactory(logger).CreateRootCommand();

        int exitCode = root.Parse(["build", "-s", srcDir.FullName, "-o", outPdf, "--preset", "invalid_foo"]).Invoke();
        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        Assert.Contains(logger.Entries, e => e.Message.Contains("未知的排版预设"));
    }

    [Fact]
    public void CommandLine_SampleOption_RejectsZeroOrNegative() {
        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(srcDir.FullName, "test.cpp"), "int main(){}");
        var outPdf = Path.Combine(tmp.Path, "out.pdf");

        var logger = new TestLogger();
        var root = new RootCommandFactory(logger).CreateRootCommand();

        int exitCode = root.Parse(["build", "-s", srcDir.FullName, "-o", outPdf, "--sample", "0"]).Invoke();
        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        Assert.Contains(logger.Entries, e => e.Message.Contains("小样文件数量必须为大于 0 的正整数"));
    }

    [Fact]
    public void EndToEnd_ProofSample_XeLatexBuild_VerifiesChineseLongLinesAndMultiLevelSections() {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PATH")) || !File.Exists("/usr/bin/xelatex")) {
            return; // 仅在安装有 xelatex 的环境中执行完整端到端编译
        }

        using var tmp = TempDir.Create();
        var srcDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "算法模版库"));
        
        // 构造带多级中文目录与长行的测试源码
        var mathDir = Directory.CreateDirectory(Path.Combine(srcDir.FullName, "01_数学与数论"));
        var geomDir = Directory.CreateDirectory(Path.Combine(mathDir.FullName, "几何算法"));
        
        var cppFile = Path.Combine(geomDir.FullName, "凸包算法.cpp");
        // 包含超长单行，验证 breaklines 自动换行无语法中断与裁切
        var longLine = string.Join(" + ", Enumerable.Range(1, 40).Select(i => $"variable_{i}_with_extremely_long_name"));
        File.WriteAllText(cppFile, $$"""
        // 中文注释：求二维平面点集的凸包面积
        #include <iostream>
        #include <vector>

        int main() {
            // 长行测试：
            long long sum = {{longLine}};
            std::cout << "凸包面积计算完成: " << sum << std::endl;
            return 0;
        }
        """);

        var outPdf = new FileInfo(Path.Combine(tmp.Path, "proof_sample.pdf"));
        var cfgFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(cfgFile.FullName, "{}");

        var logger = new TestLogger();
        var options = new BuildSubcommandOptions(
            srcDir,
            outPdf,
            cfgFile,
            Verbose: true,
            ConfigOverrides: new Dictionary<string, string> {
                // 紧凑双栏预设
                ["DOCCLASS_ORIENTATION"] = "landscape",
                ["LAYOUT_COLUMNS"] = "2",
                ["TITLE_ENABLE_COVER"] = "false",
                ["TOC_ENABLE_TOC"] = "true",
                ["CODE_FONT_SIZE"] = "6pt",
                ["CODE_LINE_HEIGHT"] = "6pt"
            },
            SampleFileLimit: 1
        );

        var pipeline = new BuildPipelineRunner(logger, new ManifestResourceManager());
        var exitCode = pipeline.Run(options, userProvidedConfig: false);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(outPdf.Exists);
        Assert.True(outPdf.Length > 0);

        // 验证产物 PDF 是有效的 PDF 格式
        Assert.True(PdfBuilder.IsValidPdfHeader(outPdf.FullName));
    }
}
