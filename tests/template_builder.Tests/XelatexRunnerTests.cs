using System;
using System.IO;
using Core;
using Xunit;

namespace template_builder.Tests;

/// <summary>
/// 真实 Process 路径的 XelatexRunner 测试。与 FakeXelatexRunner（PDF test
/// fixture）互补：验证 .NET Process API 交互本身（Start / WaitForExit /
/// stderr stream / non-zero exit propagation）的行为正确。
/// 这些测试依赖系统安装的 <c>xelatex</c>（CI 环境具备 / 本机 TeX Live）。
/// 路径 timeout 仍由 FakeXelatexRunner 覆盖（避免人工制造 hang 进程）。
/// </summary>
public class XelatexRunnerTests {
	[Fact]
	public void Run_ValidTrivialTexFile_ExitsZero() {
		var logger = new TestLogger();
		var runner = new XelatexRunner(logger);
		var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmpDir);
		var texPath = Path.Combine(tmpDir, "trivial.tex");
		File.WriteAllText(texPath, """
			\documentclass{article}
			\begin{document}
			hi
			\end{document}
			""");
		try {
			var arguments =
				$"-interaction=nonstopmode -output-directory \"{tmpDir}\" \"{texPath}\"";
			var result = runner.Run(AppContext.BaseDirectory, arguments, timeoutSeconds: 60);

			Assert.Equal(0, result.ExitCode);
			Assert.False(result.TimedOut);
			// 真实 xelatex 跑成功时 stderr 通常为空或仅含 minted 提示
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_SyntaxErrorInTexFile_ExitsNonZero() {
		var logger = new TestLogger();
		var runner = new XelatexRunner(logger);
		var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmpDir);
		var texPath = Path.Combine(tmpDir, "broken.tex");
		File.WriteAllText(texPath, """
			\documentclass{article}
			\begin{document}
			\notarealcommand
			\end{document}
			""");
		try {
			var arguments =
				$"-interaction=nonstopmode -output-directory \"{tmpDir}\" \"{texPath}\"";
			var result = runner.Run(AppContext.BaseDirectory, arguments, timeoutSeconds: 60);

			// xelatex 在语法错误时通常 exit code 1
			Assert.NotEqual(0, result.ExitCode);
			Assert.False(result.TimedOut);
			// 注意：xelatex 错误信息主要写 stdout / .log 文件，runner 的
			// stderr buffer 仅捕获 ErrorDataReceived，可能为空——这是
			// runner 的设计选择，测 exit code 就够了
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}
}
