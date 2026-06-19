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
			var generator = new CodeBlockGenerator(logger, programParser, 4, new DirectoryInfo(tempDir), new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"));

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
				var generator = new CodeBlockGenerator(logger, programParser, 4, new DirectoryInfo(tempDir), new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"));

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
				var generator = new CodeBlockGenerator(logger, programParser, 4, new DirectoryInfo(tempDir), new ManifestResourceManager(logger).GetResourceInString("Templates.CodeBlock.tex"));

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
	}
