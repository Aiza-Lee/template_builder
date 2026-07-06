using System;
using System.IO;
using Core;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class BuildFingerprintTests {
	/// <summary>
	/// 构造一个最小可用的 source 目录 + 两个 ConfigParser + 模板串，返回所有 BuildFingerprint.Compute 入参。
	/// 测试用本地 config（空 JSON），让 ConfigParser 走默认 + 用户覆盖分支；空覆盖等于"无用户配置"。
	/// </summary>
	private static (
		string SourceDir,
		string[] IgnoreGlobs,
		ConfigParser TexParser,
		ConfigParser ProgramParser,
		string MainTemplate,
		string CodeBlockTemplate,
		string MintedOutputDir
	) MakeInputs() {
		var srcDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(srcDir);
		File.WriteAllText(Path.Combine(srcDir, "a.cpp"), "int main() { return 0; }");
		var logger = new TestLogger();
		var texParser = new ConfigParser("TEX", logger);
		var programParser = new ConfigParser("PROGRAM", logger);
		texParser.ParseConfigFile("{}");
		programParser.ParseConfigFile("{}");
		return (
			srcDir,
			new[] { "*ignore*" },
			texParser,
			programParser,
			"\\documentclass{article}\\begin{document}<<CONTENT>>\\end{document}",
			"\\begin{minted}{<<LANGUAGE>>}<<CODE>>\\end{minted}",
			"/tmp/minted-cache"
		);
	}

	private static void CleanupDir(string dir) {
		try { Directory.Delete(dir, recursive: true); } catch { }
	}

	[Fact]
	public void Compute_SameInputs_SameHash() {
		var (srcDir, ignores, tex, prog, main, cb, mdir) = MakeInputs();
		try {
			var h1 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			var h2 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			Assert.Equal(h1, h2);
			Assert.Matches("^[0-9a-f]+$", h1);
		} finally {
			CleanupDir(srcDir);
		}
	}

	[Fact]
	public void Compute_DifferentSourceFile_ChangesHash() {
		var (srcDir, ignores, tex, prog, main, cb, mdir) = MakeInputs();
		try {
			var h1 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			// 改 a.cpp 的一字节
			File.WriteAllText(Path.Combine(srcDir, "a.cpp"), "int main() { return 1; }");
			var h2 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			Assert.NotEqual(h1, h2);
		} finally {
			CleanupDir(srcDir);
		}
	}

	[Fact]
	public void Compute_DifferentConfigValue_ChangesHash() {
		var (srcDir, ignores, tex, prog, main, cb, mdir) = MakeInputs();
		try {
			var h1 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			var logger = new TestLogger();
			var tex2 = new ConfigParser("TEX", logger);
			tex2.ParseConfigFile("""{ "TEX": { "title": { "content": "NewTitle" } } }""");
			var h2 = BuildFingerprint.Compute(srcDir, ignores, tex2, prog, main, cb, mdir);
			Assert.NotEqual(h1, h2);
		} finally {
			CleanupDir(srcDir);
		}
	}

	[Fact]
	public void Compute_DifferentTemplate_ChangesHash() {
		var (srcDir, ignores, tex, prog, main, cb, mdir) = MakeInputs();
		try {
			var h1 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			var h2 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main + "% extra", cb, mdir);
			Assert.NotEqual(h1, h2);
		} finally {
			CleanupDir(srcDir);
		}
	}

	[Fact]
	public void Compute_IgnoresHiddenAndIgnoredFiles() {
		var (srcDir, ignores, tex, prog, main, cb, mdir) = MakeInputs();
		try {
			var h1 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			// 加一个隐藏文件 + 一个匹配 ignore glob 的文件
			File.WriteAllText(Path.Combine(srcDir, ".hidden"), "secret");
			File.WriteAllText(Path.Combine(srcDir, "ignore_me.cpp"), "// ignore target");
			var h2 = BuildFingerprint.Compute(srcDir, ignores, tex, prog, main, cb, mdir);
			Assert.Equal(h1, h2);
		} finally {
			CleanupDir(srcDir);
		}
	}

	[Fact]
	public void WriteAndReadSidecar_RoundTrips() {
		var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tbuild");
		try {
			const string hash = "deadbeef12345678";
			BuildFingerprint.WriteSidecar(tmp, hash);
			Assert.True(File.Exists(tmp));
			Assert.True(BuildFingerprint.TryLoadSidecar(tmp, out var read));
			Assert.Equal(hash, read);
		} finally {
			if (File.Exists(tmp)) File.Delete(tmp);
		}
	}

	[Fact]
	public void WriteSidecar_OverwritesExisting() {
		var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tbuild");
		try {
			BuildFingerprint.WriteSidecar(tmp, "first");
			BuildFingerprint.WriteSidecar(tmp, "second");
			Assert.True(BuildFingerprint.TryLoadSidecar(tmp, out var read));
			Assert.Equal("second", read);
		} finally {
			if (File.Exists(tmp)) File.Delete(tmp);
		}
	}
}
