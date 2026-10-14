using System.IO;
using Core;
using Core.Models;
using Core.SourceTree;
using Core.Tui;
using Spectre.Console;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class SourceTreePlanTests {
    [Fact]
    public void NaturalSort_EnsuresTenComesAfterTwo() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "10_algo.py"), "# 10");
        File.WriteAllText(Path.Combine(tmp.Path, "2_algo.py"), "# 2");
        File.WriteAllText(Path.Combine(tmp.Path, "1_algo.py"), "# 1");
        File.WriteAllText(Path.Combine(tmp.Path, "20_algo.py"), "# 20");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        var names = plan.IncludedFiles.Select(f => f.File.Name).ToList();
        Assert.Equal(["1_algo.py", "2_algo.py", "10_algo.py", "20_algo.py"], names);
    }

    [Fact]
    public void SectionTitleOverrides_RenamesSectionTitleWithoutChangingDiskFile() {
        using var tmp = TempDir.Create();
        var mathDir = Path.Combine(tmp.Path, "math");
        Directory.CreateDirectory(mathDir);
        File.WriteAllText(Path.Combine(mathDir, "01_intro.py"), "print('intro')");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        progParser.ParseConfigFile("""
        {
            "PROGRAM": {
                "section_title_overrides": [
                    "01_intro.py:系统架构引言",
                    "math:核心数学算法库"
                ]
            }
        }
        """);

        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);
        Assert.Single(plan.IncludedFiles);
        var file = plan.IncludedFiles[0];
        Assert.Equal("系统架构引言", file.DisplayTitle);
        Assert.Equal("01_intro.py", file.File.Name);

        var generator = new CodeBlockGenerator(
            logger, progParser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true
        );
        var tex = generator.Generate();
        Assert.Contains(@"\section{核心数学算法库}", tex);
        Assert.Contains(@"\subsection{系统架构引言}", tex);
        Assert.Contains("% [TB_SOURCE_FILE: math/01_intro.py]", tex);
    }

    [Fact]
    public void SectionOrderOverrides_PrioritizesConfiguredWeights() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "1.py"), "# 1");
        File.WriteAllText(Path.Combine(tmp.Path, "2.py"), "# 2");
        File.WriteAllText(Path.Combine(tmp.Path, "10.py"), "# 10");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        progParser.ParseConfigFile("""
        {
            "PROGRAM": {
                "section_order_overrides": [
                    "10.py:1",
                    "1.py:2"
                ]
            }
        }
        """);

        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);
        var names = plan.IncludedFiles.Select(f => f.File.Name).ToList();
        // 10.py (weight 1) -> 1.py (weight 2) -> 2.py (weight MaxValue, natural sort)
        Assert.Equal(["10.py", "1.py", "2.py"], names);
    }

    [Fact]
    public void ExcludedFiles_ExplainDistinctionBetweenRecognizedAndUnknownLanguages() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "main.cpp"), "int main() {}");
        File.WriteAllText(Path.Combine(tmp.Path, "tool.swift"), "import Foundation"); // 系统内置支持但未在 include_file_types
        File.WriteAllText(Path.Combine(tmp.Path, "data.unknownext"), "raw data"); // 未知类型

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        Assert.Single(plan.IncludedFiles);
        Assert.Equal("main.cpp", plan.IncludedFiles[0].File.Name);

        var swiftExcluded = plan.ExcludedFiles.FirstOrDefault(f => f.Info.Name == "tool.swift");
        Assert.NotNull(swiftExcluded);
        Assert.Equal(SourceExclusionReason.ExtensionNotIncluded, swiftExcluded.Reason);
        Assert.Contains("swift", swiftExcluded.Explanation);
        Assert.Contains("include_file_types", swiftExcluded.Explanation);

        var unknownExcluded = plan.ExcludedFiles.FirstOrDefault(f => f.Info.Name == "data.unknownext");
        Assert.NotNull(unknownExcluded);
        Assert.Equal(SourceExclusionReason.ExtensionNotIncluded, unknownExcluded.Reason);
        Assert.DoesNotContain("系统已支持该语言", unknownExcluded.Explanation);
    }

    [Fact]
    public void EmptyDirectory_IsFlaggedAsExcluded() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "empty_dir"));
        File.WriteAllText(Path.Combine(tmp.Path, "code.py"), "print(1)");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        var emptyDir = plan.ExcludedDirectories.FirstOrDefault(d => d.Info.Name == "empty_dir");
        Assert.NotNull(emptyDir);
        Assert.Equal(SourceExclusionReason.EmptyDirectory, emptyDir.Reason);
    }

    [Fact]
    public void CodeBlockGenerator_UsesExactSamePlanAndFileSequence() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "sub"));
        File.WriteAllText(Path.Combine(tmp.Path, "sub", "2.cpp"), "int b;");
        File.WriteAllText(Path.Combine(tmp.Path, "sub", "10.cpp"), "int c;");
        File.WriteAllText(Path.Combine(tmp.Path, "sub", "1.cpp"), "int a;");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        var generator = new CodeBlockGenerator(
            logger, progParser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true
        );
        var tex = generator.Generate();

        // 验证文件在 TeX 中按 plan 的确切顺序出现
        int lastIndex = -1;
        foreach (var file in plan.IncludedFiles) {
            var marker = $"% [TB_SOURCE_FILE: {file.RelativePath}]";
            var idx = tex.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(idx > lastIndex, $"文件 {file.RelativePath} 未按预期顺序出现。");
            lastIndex = idx;
        }
    }

    [Fact]
    public void RenderSourceTreePreview_RendersSuccessfullyInAllModes() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "01_test.py"), "print(1)");
        File.WriteAllText(Path.Combine(tmp.Path, "test.rs"), "fn main() {}");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        foreach (var mode in new[] { SourceTreeFilterMode.All, SourceTreeFilterMode.IncludedOnly, SourceTreeFilterMode.ExcludedOnly }) {
            var output = TuiScaffolding.RenderToPlainText(c => {
                TuiViewRenderer.RenderSourceTreePreview(c, plan, mode);
            });

            Assert.Contains("源码收录与章节结构预检面板", output);
            Assert.Contains(tmp.Path, output);
        }
    }

    [Fact]
    public void Build_SymlinkLoop_DetectedAndExcludedWithoutHanging() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "main.cpp"), "int main() {}");

        var loopPath = Path.Combine(tmp.Path, "loop_dir");
        bool created = false;
        try {
            Directory.CreateSymbolicLink(loopPath, tmp.Path);
            created = true;
        } catch {
            // OS may not support symlink creation without admin rights
        }

        if (created) {
            var logger = new TestLogger();
            var progParser = new ConfigParser("PROGRAM", logger);
            var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

            Assert.Single(plan.IncludedFiles);
            var loopExcluded = plan.ExcludedDirectories.FirstOrDefault(d => d.Info.Name == "loop_dir");
            Assert.NotNull(loopExcluded);
            Assert.Equal(SourceExclusionReason.SymlinkLoop, loopExcluded.Reason);
            Assert.Contains("循环引用", loopExcluded.Explanation);
        }
    }

    [Fact]
    public void Build_ExternalDirectorySymlink_DetectedAndExcluded() {
        using var tmp = TempDir.Create();
        using var outsideTmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "main.cpp"), "int main() {}");
        File.WriteAllText(Path.Combine(outsideTmp.Path, "external.cpp"), "int ext() {}");

        var extLinkPath = Path.Combine(tmp.Path, "ext_link");
        bool created = false;
        try {
            Directory.CreateSymbolicLink(extLinkPath, outsideTmp.Path);
            created = true;
        } catch {
            // OS may not support symlinks
        }

        if (created) {
            var logger = new TestLogger();
            var progParser = new ConfigParser("PROGRAM", logger);
            var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

            Assert.Single(plan.IncludedFiles);
            Assert.Equal("main.cpp", plan.IncludedFiles[0].File.Name);

            var extExcluded = plan.ExcludedDirectories.FirstOrDefault(d => d.Info.Name == "ext_link");
            Assert.NotNull(extExcluded);
            Assert.Equal(SourceExclusionReason.ExternalSymlink, extExcluded.Reason);
            Assert.Contains("外部", extExcluded.Explanation);
        }
    }

    [Fact]
    public void Build_UnincludedDeepFiles_DoNotInflateMaxDepth() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "root.cpp"), "int main() {}");

        var current = tmp.Path;
        for (int i = 0; i < 6; i++) {
            current = Path.Combine(current, $"level_{i}");
            Directory.CreateDirectory(current);
        }
        File.WriteAllText(Path.Combine(current, "ignored.bin"), "0101");

        var logger = new TestLogger();
        var progParser = new ConfigParser("PROGRAM", logger);
        var plan = SourceTreePlan.Build(new DirectoryInfo(tmp.Path), progParser, sectionDepth: 5, logger);

        Assert.Single(plan.IncludedFiles);
        Assert.Equal(0, plan.MaxDepth);
        Assert.Equal(0, plan.IncludedFiles[0].Depth);
    }
}
