using System;
using System.IO;
using Core;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class CodeBlockGeneratorTests {
	[Fact]
		public void Generate_SortsDirectoriesAndFilesAlphabetically_AndEscapesSectionNames() {
		var tempDir = CreateTempDirectory();
		try {
			Directory.CreateDirectory(Path.Combine(tempDir, "zeta-folder"));
			Directory.CreateDirectory(Path.Combine(tempDir, "Alpha_Folder"));

			File.WriteAllText(Path.Combine(tempDir, "zeta-folder", "z_file.txt"), "z sub file");
			File.WriteAllText(Path.Combine(tempDir, "Alpha_Folder", "a_file.txt"), "a sub file");

			File.WriteAllText(Path.Combine(tempDir, "zeta.txt"), "root z");
			File.WriteAllText(Path.Combine(tempDir, "Alpha.txt"), "root a");
			File.WriteAllText(Path.Combine(tempDir, "notes.md"), "should be skipped");

			var logger = new TestLogger();
			var programParser = new ConfigParser("PROGRAM", logger);
			var generator = new CodeBlockGenerator(
				logger, programParser, 4, new DirectoryInfo(tempDir),
				new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
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
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void Generate_UsesCustomLanguageDirectiveForJsonAndTypeScript() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "config.json"), "{ \"value\": 1 }");
				File.WriteAllText(Path.Combine(tempDir, "script.ts"), "const value: number = 42;");

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
					logger, programParser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);

				var output = generator.Generate();

				Assert.Contains(@"\begin{minted}{json}", output);
				Assert.Contains(@"\begin{minted}{typescript}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void Generate_HonorsIgnorePatterns_AsGlobs() {
			var tempDir = CreateTempDirectory();
			try {
				// 模拟 a.tmp / 目录 build/ 应被排除；keep.txt 保留
				Directory.CreateDirectory(Path.Combine(tempDir, "build"));
				File.WriteAllText(Path.Combine(tempDir, "a.tmp"), "ignored by glob");
				File.WriteAllText(Path.Combine(tempDir, "keep.txt"), "kept");
				File.WriteAllText(Path.Combine(tempDir, "build", "x.txt"), "ignored by dir glob");

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
					logger, programParser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);

				var output = generator.Generate();

				Assert.Contains(@"\section{keep.txt}", output);
				Assert.DoesNotContain(@"\section{a.tmp}", output);
				Assert.DoesNotContain(@"\section{build}", output);
				Assert.DoesNotContain("ignored by glob", output);
				Assert.DoesNotContain("ignored by dir glob", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}
		private static string CreateTempDirectory() {
			var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			return Directory.CreateDirectory(directoryPath).FullName;
		}

		// ============================================================
		//  Round 3a tests: language_overrides / section_depth / escape_section_names
		// ============================================================

		[Fact]
		public void LanguageOverride_ExtensionInWhitelistNotInDefault_AddsToMap() {
			var tempDir = CreateTempDirectory();
			try {
				// .c 在 CODE_LANGUAGES_EXTENSIONS 白名单（→ 走 minted）但不在 _defaultExtMap（→ 默认 PlainText）。
				// 用户的 override 给 .c 指派 cpp lexer，应直接生效。
				File.WriteAllText(Path.Combine(tempDir, "a.c"), "int main(){}");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{cpp}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void LanguageOverride_ExistingExtension_OverridesDefault() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "a.py"), "x = 1");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{py3}", output);
				Assert.DoesNotContain(@"\begin{minted}{python}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void LanguageOverride_DuplicateKey_LatterWins() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "a.py"), "x = 1");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{second}", output);
				Assert.DoesNotContain(@"\begin{minted}{first}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void LanguageOverride_MissingColon_SilentlySkipped() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "a.py"), "x = 1");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{python}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void LanguageOverride_NoDotPrefix_LogsWarningAndSkips() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "a.py"), "x = 1");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{python}", output);
				Assert.Contains(logger.Entries, e =>
					e.Level == LogLevel.WARNING && e.Message.Contains("py:python"));
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void LanguageOverride_EmptyList_DefaultMapUnchanged() {
			var tempDir = CreateTempDirectory();
			try {
				File.WriteAllText(Path.Combine(tempDir, "a.py"), "x = 1");
				File.WriteAllText(Path.Combine(tempDir, "a.cpp"), "int main(){}");
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
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\begin{minted}{python}", output);
				Assert.Contains(@"\begin{minted}{cpp}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void SectionDepth_Three_ClampsDeeperLevelsToLast() {
			var tempDir = CreateTempDirectory();
			try {
				// 5 层目录嵌套，sectionDepth=3 → 深度 3/4 全部 clamp 到 subsubsection
				Directory.CreateDirectory(Path.Combine(tempDir, "a"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b", "c"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b", "c", "d"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b", "c", "d", "e"));
				File.WriteAllText(Path.Combine(tempDir, "a", "b", "c", "d", "e", "leaf.txt"), "x");
				var logger = new TestLogger();
				var parser = new ConfigParser("PROGRAM", logger);
				var generator = new CodeBlockGenerator(
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 3, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\section{a}", output);
				Assert.Contains(@"\subsection{b}", output);
				Assert.Contains(@"\subsubsection{c}", output);
				Assert.Contains(@"\subsubsection{d}", output);
				Assert.Contains(@"\subsubsection{e}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void SectionDepth_One_AllLevelsUseSection() {
			var tempDir = CreateTempDirectory();
			try {
				Directory.CreateDirectory(Path.Combine(tempDir, "a"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b", "c"));
				File.WriteAllText(Path.Combine(tempDir, "a", "b", "c", "leaf.txt"), "x");
				var logger = new TestLogger();
				var parser = new ConfigParser("PROGRAM", logger);
				var generator = new CodeBlockGenerator(
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 1, escapeSectionNames: true);
				var output = generator.Generate();
				var sectionCount = System.Text.RegularExpressions.Regex.Matches(output, @"\\section\{").Count;
				Assert.True(sectionCount >= 4, $"Expected >= 4 \\section{{ invocations, got {sectionCount}");
				Assert.DoesNotContain(@"\subsection{", output);
				Assert.DoesNotContain(@"\subsubsection{", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void SectionDepth_Five_MatchesOriginalBehavior() {
			var tempDir = CreateTempDirectory();
			try {
				Directory.CreateDirectory(Path.Combine(tempDir, "a"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b"));
				Directory.CreateDirectory(Path.Combine(tempDir, "a", "b", "c"));
				File.WriteAllText(Path.Combine(tempDir, "a", "b", "c", "leaf.txt"), "x");
				var logger = new TestLogger();
				var parser = new ConfigParser("PROGRAM", logger);
				var generator = new CodeBlockGenerator(
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\section{a}", output);
				Assert.Contains(@"\subsection{b}", output);
				Assert.Contains(@"\subsubsection{c}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void EscapeSectionNames_True_UnderscoresInFolderName() {
			var tempDir = CreateTempDirectory();
			try {
				Directory.CreateDirectory(Path.Combine(tempDir, "my_folder"));
				File.WriteAllText(Path.Combine(tempDir, "my_folder", "leaf.txt"), "x");
				var logger = new TestLogger();
				var parser = new ConfigParser("PROGRAM", logger);
				var generator = new CodeBlockGenerator(
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: true);
				var output = generator.Generate();
				Assert.Contains(@"\section{my\_folder}", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}

		[Fact]
		public void EscapeSectionNames_False_PassesThroughUnderscoreRaw() {
			var tempDir = CreateTempDirectory();
			try {
				Directory.CreateDirectory(Path.Combine(tempDir, "my_folder"));
				File.WriteAllText(Path.Combine(tempDir, "my_folder", "leaf.txt"), "x");
				var logger = new TestLogger();
				var parser = new ConfigParser("PROGRAM", logger);
				var generator = new CodeBlockGenerator(
					logger, parser, 4, new DirectoryInfo(tempDir),
					new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"),
					sectionDepth: 5, escapeSectionNames: false);
				var output = generator.Generate();
				Assert.Contains(@"\section{my_folder}", output);
				Assert.DoesNotContain(@"my\_folder", output);
			} finally {
				Directory.Delete(tempDir, true);
			}
		}
	}

