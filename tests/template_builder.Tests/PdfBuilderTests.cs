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

	private static string CreateTempDir() =>
		Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
		).FullName;
}
