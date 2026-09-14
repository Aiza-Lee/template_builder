using System;
using System.IO;
using Core;
using Microsoft.Extensions.FileSystemGlobbing;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class CodeBlockGeneratorTests {
    [Fact]
        public void Generate_SortsDirectoriesAndFilesAlphabetically_AndEscapesSectionNames() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "zeta-folder"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "Alpha_Folder"));

        File.WriteAllText(Path.Combine(tmp.Path, "zeta-folder", "z_file.txt"), "z sub file");
        File.WriteAllText(Path.Combine(tmp.Path, "Alpha_Folder", "a_file.txt"), "a sub file");

        File.WriteAllText(Path.Combine(tmp.Path, "zeta.txt"), "root z");
        File.WriteAllText(Path.Combine(tmp.Path, "Alpha.txt"), "root a");
        File.WriteAllText(Path.Combine(tmp.Path, "notes.md"), "should be skipped");

        var logger = new TestLogger();
        var programParser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, programParser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);

        var output = generator.Generate();

        Assert.Contains(@"\section{Alpha\_Folder}", output);
        Assert.Contains(@"\section{zeta-folder}", output);
        Assert.Contains(@"\section{Alpha.txt}", output);
        Assert.Contains(@"\section{zeta.txt}", output);
        Assert.Contains(@"\subsection{a\_file.txt}", output);
        Assert.Contains(@"\subsection{z\_file.txt}", output);
        Assert.DoesNotContain("notes.md", output, StringComparison.OrdinalIgnoreCase);

        var alphaDirIndex = output.IndexOf(@"\section{Alpha\_Folder}", StringComparison.Ordinal);
        var zetaDirIndex = output.IndexOf(@"\section{zeta-folder}", StringComparison.Ordinal);
        Assert.True(alphaDirIndex >= 0 && zetaDirIndex >= 0 && alphaDirIndex < zetaDirIndex);

        var alphaFileIndex = output.IndexOf(@"\section{Alpha.txt}", StringComparison.Ordinal);
        var zetaFileIndex = output.IndexOf(@"\section{zeta.txt}", StringComparison.Ordinal);
        Assert.True(alphaFileIndex >= 0 && zetaFileIndex >= 0 && alphaFileIndex < zetaFileIndex);
    }

        [Fact]
        public void Generate_UsesCustomLanguageDirectiveForJsonAndTypeScript() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "config.json"), "{ \"value\": 1 }");
        File.WriteAllText(Path.Combine(tmp.Path, "script.ts"), "const value: number = 42;");

        var logger = new TestLogger();
        var programParser = new ConfigParser("PROGRAM", logger);
        programParser.ParseConfigFile(
            """
            {
                "PROGRAM": {
                "include_file_types": [ ".json", ".ts" ]
                }
            }
            """
        );
        var generator = new CodeBlockGenerator(
            logger, programParser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);

        var output = generator.Generate();

        Assert.Contains(@"\begin{minted}{json}", output);
        Assert.Contains(@"\begin{minted}{typescript}", output);
    }

        [Fact]
        public void Generate_HonorsIgnorePatterns_AsGlobs() {
        using var tmp = TempDir.Create();
        // 模拟 a.tmp / 目录 build/ 应被排除；keep.txt 保留
        Directory.CreateDirectory(Path.Combine(tmp.Path, "build"));
        File.WriteAllText(Path.Combine(tmp.Path, "a.tmp"), "ignored by glob");
        File.WriteAllText(Path.Combine(tmp.Path, "keep.txt"), "kept");
        File.WriteAllText(Path.Combine(tmp.Path, "build", "x.txt"), "ignored by dir glob");

        var logger = new TestLogger();
        var programParser = new ConfigParser("PROGRAM", logger);
        programParser.ParseConfigFile(
            """
            {
                "PROGRAM": {
                "include_file_types": [ ".tmp", ".txt" ],
                "ignore_patterns": [ "*.tmp", "build" ]
                }
            }
            """
        );
        var generator = new CodeBlockGenerator(
            logger, programParser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);

        var output = generator.Generate();

        Assert.Contains(@"\section{keep.txt}", output);
        Assert.DoesNotContain(@"\section{a.tmp}", output);
        Assert.DoesNotContain(@"\section{build}", output);
        Assert.DoesNotContain("ignored by glob", output);
        Assert.DoesNotContain("ignored by dir glob", output);
    }

    // ============================================================
    //  Round 3a tests: language_overrides / section_depth / escape_section_names
    // ============================================================

    [Fact]
    public void LanguageOverride_ReplacesDefaultForKnownExtension() {
        using var tmp = TempDir.Create();
        // .c 默认映射到 minted "c"；override .c:cpp 应替换默认值并以 cpp 渲染。
        File.WriteAllText(Path.Combine(tmp.Path, "a.c"), "int main(){}");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".c" ],
                "code_language_overrides": [ ".c:cpp" ]
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{cpp}", output);
    }

    [Fact]
    public void LanguageOverride_ExistingExtension_OverridesDefault() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.py"), "x = 1");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".py" ],
                "code_language_overrides": [ ".py:py3" ]
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{py3}", output);
        Assert.DoesNotContain(@"\begin{minted}{python}", output);
    }

    [Fact]
    public void LanguageOverride_DuplicateKey_LatterWins() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.py"), "x = 1");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".py" ],
                "code_language_overrides": [ ".py:first", ".py:second" ]
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{second}", output);
        Assert.DoesNotContain(@"\begin{minted}{first}", output);
    }

    [Fact]
    public void LanguageOverride_MissingColon_SilentlySkipped() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.py"), "x = 1");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".py" ],
                "code_language_overrides": [ ".py=python" ]
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{python}", output);
    }

    [Fact]
    public void LanguageOverride_NoDotPrefix_LogsWarningAndSkips() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.py"), "x = 1");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".py" ],
                "code_language_overrides": [ "py:python" ]
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{python}", output);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.WARNING && e.Message.Contains("py:python"));
    }

    [Fact]
    public void LanguageOverride_EmptyList_DefaultMapUnchanged() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.py"), "x = 1");
        File.WriteAllText(Path.Combine(tmp.Path, "a.cpp"), "int main(){}");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
            {
                "PROGRAM": {
                "include_file_types": [ ".py", ".cpp" ],
                "code_language_overrides": []
                }
            }
            """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\begin{minted}{python}", output);
        Assert.Contains(@"\begin{minted}{cpp}", output);
    }

    [Fact]
    public void SectionDepth_Three_ClampsDeeperLevelsToLast() {
        using var tmp = TempDir.Create();
        // 5 层目录嵌套，sectionDepth=3 → 深度 3/4 全部 clamp 到 subsubsection
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b", "c"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b", "c", "d"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b", "c", "d", "e"));
        File.WriteAllText(Path.Combine(tmp.Path, "a", "b", "c", "d", "e", "leaf.txt"), "x");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 3, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\section{a}", output);
        Assert.Contains(@"\subsection{b}", output);
        Assert.Contains(@"\subsubsection{c}", output);
        Assert.Contains(@"\subsubsection{d}", output);
        Assert.Contains(@"\subsubsection{e}", output);
    }

    [Fact]
    public void SectionDepth_One_AllLevelsUseSection() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b", "c"));
        File.WriteAllText(Path.Combine(tmp.Path, "a", "b", "c", "leaf.txt"), "x");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 1, escapeSectionNames: true);
        var output = generator.Generate();
        var sectionCount = System.Text.RegularExpressions.Regex.Matches(output, @"\\section\{").Count;
        Assert.True(sectionCount >= 4, $"Expected >= 4 \\section{{ invocations, got {sectionCount}");
        Assert.DoesNotContain(@"\subsection{", output);
        Assert.DoesNotContain(@"\subsubsection{", output);
    }

    [Fact]
    public void SectionDepth_Five_MatchesOriginalBehavior() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "a", "b", "c"));
        File.WriteAllText(Path.Combine(tmp.Path, "a", "b", "c", "leaf.txt"), "x");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\section{a}", output);
        Assert.Contains(@"\subsection{b}", output);
        Assert.Contains(@"\subsubsection{c}", output);
    }

    [Fact]
    public void EscapeSectionNames_True_UnderscoresInFolderName() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "my_folder"));
        File.WriteAllText(Path.Combine(tmp.Path, "my_folder", "leaf.txt"), "x");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.Contains(@"\section{my\_folder}", output);
    }

    [Fact]
    public void EscapeSectionNames_False_PassesThroughUnderscoreRaw() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "my_folder"));
        File.WriteAllText(Path.Combine(tmp.Path, "my_folder", "leaf.txt"), "x");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: false);
        var output = generator.Generate();
        Assert.Contains(@"\section{my_folder}", output);
        Assert.DoesNotContain(@"my\_folder", output);
    }

    // Round 3d: tab_size=0 不应触发 DivideByZero；fallback 到 tab_size=1。
    [Fact]
    public void ExpandTabs_TabSizeZero_DoesNotCrash() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "a.txt"), "\thello");
        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        var generator = new CodeBlockGenerator(
            logger, parser, tabSize: 0, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();
        Assert.NotNull(output);
        Assert.NotEmpty(output);
        // tab_size=0 fallback 到 1 → \t 展开为 1 个空格 → " hello"
        Assert.Contains(" hello", output);
    }

    [Fact]
    public void Generate_SourceDirContainsBuildDirWithoutIgnorePattern_BuildDirIsSkipped() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "build"));
        File.WriteAllText(Path.Combine(tmp.Path, "build", "mid-output.tex"), "latex content");
        File.WriteAllText(Path.Combine(tmp.Path, "code.cpp"), "int main() {}");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".cpp", ".tex" ],
                "ignore_patterns": []
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        Assert.Contains(@"\section{code.cpp}", output);
        Assert.DoesNotContain(@"\section{build}", output);
        Assert.DoesNotContain(@"\section{mid-output.tex}", output);
    }

    [Fact]
    public void Generate_HonorsHierarchicalIgnorePatterns_SuchAsSubdirsAndPathGlobs() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "docs"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "src", "legacy"));
        Directory.CreateDirectory(Path.Combine(tmp.Path, "src", "modern"));

        File.WriteAllText(Path.Combine(tmp.Path, "docs", "readme.txt"), "documentation");
        File.WriteAllText(Path.Combine(tmp.Path, "src", "legacy", "old.cpp"), "legacy code");
        File.WriteAllText(Path.Combine(tmp.Path, "src", "modern", "new.cpp"), "modern code");
        File.WriteAllText(Path.Combine(tmp.Path, "src", "modern", "draft.tmp"), "draft");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".cpp", ".txt", ".tmp" ],
                "ignore_patterns": [ "docs/**", "src/legacy/**", "*.tmp" ]
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        // modern code should be included (under \section{src} -> \subsection{modern} -> \subsubsection{new.cpp})
        Assert.Contains(@"\section{src}", output);
        Assert.Contains(@"\subsection{modern}", output);
        Assert.Contains(@"\subsubsection{new.cpp}", output);
        Assert.Contains("modern code", output);

        // docs/** should be completely excluded
        Assert.DoesNotContain("docs", output);
        Assert.DoesNotContain("documentation", output);

        // src/legacy/** should be completely excluded
        Assert.DoesNotContain("legacy", output);
        Assert.DoesNotContain("old.cpp", output);
        Assert.DoesNotContain("legacy code", output);

        // *.tmp in subdirectories should be excluded
        Assert.DoesNotContain("draft.tmp", output);
        Assert.DoesNotContain("draft", output);
    }

    [Fact]
    public void Generate_HonorsSingleStarInDirectory() {
        using var tmp = TempDir.Create();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "docs"));
        File.WriteAllText(Path.Combine(tmp.Path, "docs", "readme.txt"), "documentation");
        File.WriteAllText(Path.Combine(tmp.Path, "code.cpp"), "int main() {}");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".cpp", ".txt" ],
                "ignore_patterns": [ "docs/*" ]
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        Assert.Contains(@"\section{code.cpp}", output);
        Assert.DoesNotContain("documentation", output);
        Assert.DoesNotContain("readme.txt", output);
    }

    [Fact]
    public void Generate_UsesMintedForJavaAndCppExtensions() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "Solution.java"), "public class Solution {}");
        File.WriteAllText(Path.Combine(tmp.Path, "algo.cc"), "int main() {}");
        File.WriteAllText(Path.Combine(tmp.Path, "math.cxx"), "int gcd() {}");
        File.WriteAllText(Path.Combine(tmp.Path, "tree.hxx"), "struct Node {};");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".java", ".cc", ".cxx", ".hxx" ]
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        Assert.Contains(@"\begin{minted}{java}", output);
        Assert.Contains(@"\begin{minted}{cpp}", output);
        Assert.DoesNotContain(@"\begin{minted}{text}", output);
    }

    [Fact]
    public void Generate_PrunesEmptyDirectories_DoesNotEmitEmptySections() {
        using var tmp = TempDir.Create();
        // 1. 完全空的根目录
        Directory.CreateDirectory(Path.Combine(tmp.Path, "empty_dir"));
        // 2. 嵌套空目录
        Directory.CreateDirectory(Path.Combine(tmp.Path, "nested_empty", "deep_empty"));
        // 3. 仅含不支持类型文件的目录
        Directory.CreateDirectory(Path.Combine(tmp.Path, "unsupported_dir"));
        File.WriteAllText(Path.Combine(tmp.Path, "unsupported_dir", "image.png"), "binary data");
        // 4. 含有效源码文件的目录与子目录
        Directory.CreateDirectory(Path.Combine(tmp.Path, "valid_dir", "sub_valid"));
        File.WriteAllText(Path.Combine(tmp.Path, "valid_dir", "sub_valid", "algo.cpp"), "int main() {}");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".cpp" ]
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        // 验证有效目录及其代码正常输出
        Assert.Contains(@"\section{valid\_dir}", output);
        Assert.Contains(@"\subsection{sub\_valid}", output);
        Assert.Contains(@"\subsubsection{algo.cpp}", output);
        Assert.Contains("int main() {}", output);

        // 验证空目录及无有效代码的目录被彻底剪枝
        Assert.DoesNotContain("empty_dir", output);
        Assert.DoesNotContain("nested_empty", output);
        Assert.DoesNotContain("deep_empty", output);
        Assert.DoesNotContain("unsupported_dir", output);
    }

    [Fact]
    public void Generate_MatchesFileExtensionsCaseInsensitively_WithMintedLanguage() {
        using var tmp = TempDir.Create();
        File.WriteAllText(Path.Combine(tmp.Path, "Solution.CPP"), "int main() { return 0; }");
        File.WriteAllText(Path.Combine(tmp.Path, "Script.Py"), "print('hello')");
        File.WriteAllText(Path.Combine(tmp.Path, "Header.HPP"), "#pragma once");

        var logger = new TestLogger();
        var parser = new ConfigParser("PROGRAM", logger);
        // 配置中使用大写和混合大小写
        parser.ParseConfigFile("""
        {
            "PROGRAM": {
                "include_file_types": [ ".cpp", ".PY", ".hpp" ]
            }
        }
        """);
        var generator = new CodeBlockGenerator(
            logger, parser, 4, new DirectoryInfo(tmp.Path),
            new ManifestResourceManager().GetResourceInString("Templates.CodeBlock.tex"),
            sectionDepth: 5, escapeSectionNames: true);
        var output = generator.Generate();

        Assert.Contains(@"\begin{minted}{cpp}", output);
        Assert.Contains(@"\begin{minted}{python}", output);
        Assert.Contains(@"int main() { return 0; }", output);
        Assert.Contains(@"print('hello')", output);
        Assert.Contains(@"#pragma once", output);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.WARNING && e.Message.Contains("not in the include list"));
    }
}
