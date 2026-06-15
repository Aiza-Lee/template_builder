using System;
using System.IO;
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

	private static string CreateTempDir() =>
		Directory.CreateDirectory(
			Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
		).FullName;
}
