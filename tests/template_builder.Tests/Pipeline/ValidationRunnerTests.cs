using System.IO;
using System.Text.Json;
using Core;
using Core.Pipeline;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ValidationRunnerTests {
	private static string CreateTempDir() {
		return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
	}

	private static (string tmpDir, DirectoryInfo sourceDir, FileInfo configFile) CreateHappyPathFixture() {
		var tmpDir = CreateTempDir();
		var sourceDir = new DirectoryInfo(Path.Combine(tmpDir, "src"));
		sourceDir.Create();
		File.WriteAllText(Path.Combine(sourceDir.FullName, "a.txt"), "alpha");
		File.WriteAllText(Path.Combine(sourceDir.FullName, "b.txt"), "beta");
		var configFile = new FileInfo(Path.Combine(tmpDir, "config.json"));
		// 默认 DefaultConfig 即可：包含 .txt 的 include_file_types 与空的 ignore_patterns
		File.WriteAllText(configFile.FullName, "{}");
		return (tmpDir, sourceDir, configFile);
	}

	[Fact]
	public void Run_HappyPath_EmptySource_ReturnsOk() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			sourceDir.Delete(recursive: true);
			sourceDir.Create();  // 重新建一个空目录
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

			Assert.Equal(0, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_NonExistentSourceDir_ReturnsInvalidArguments() {
		var (tmpDir, _, configFile) = CreateHappyPathFixture();
		try {
			var missingDir = new DirectoryInfo(Path.Combine(tmpDir, "does-not-exist"));
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(missingDir, configFile));

			Assert.Equal(2, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_MalformedConfig_ReturnsMalformedConfig() {
		var (tmpDir, sourceDir, _) = CreateHappyPathFixture();
		try {
			var configFile = new FileInfo(Path.Combine(tmpDir, "bad.json"));
			File.WriteAllText(configFile.FullName, "{ \"TEX\": {");
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

			Assert.Equal(4, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_UnknownConfigKey_ReturnsInvalidArguments() {
		var (tmpDir, sourceDir, _) = CreateHappyPathFixture();
		try {
			var configFile = new FileInfo(Path.Combine(tmpDir, "cfg.json"));
			File.WriteAllText(configFile.FullName, """{ "TEX": { "totally_made_up_key": "oops" } }""");
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

			Assert.Equal(2, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_MissingEmbeddedResource_ReturnsMissingEmbeddedResource() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			var resMgr = new ThrowingResMgr(new TestLogger());
			var runner = new ValidationRunner(new TestLogger(), resMgr);

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

			Assert.Equal(5, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_MissingPlaceholderInTemplateDir_ReturnsUnresolvedPlaceholders() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			// 用一个外部 template-dir 提供带 ##MISSING## 的 Main.tex
			var templateDir = Directory.CreateDirectory(Path.Combine(tmpDir, "templates"));
			File.WriteAllText(Path.Combine(templateDir.FullName, "Main.tex"),
				"\\documentclass{article}\\title{##MISSING##}\\begin{document}##AUTHOR##\\end{document}");
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, templateDir));

			Assert.Equal(3, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_SixLevelDeepDirectory_ReturnsValidationFailed() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			// 建 6 层深目录：src/a/b/c/d/e/f/
			var deep = sourceDir.FullName;
			for (int i = 0; i < 6; i++) {
				deep = Path.Combine(deep, $"level{i}");
				Directory.CreateDirectory(deep);
			}
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

			Assert.Equal(6, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_TextFormat_ContainsOkAndErrorMarkers() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager(new TestLogger()));
			var logger = new TestLogger();

			// 重新构造以拿 logger
			runner = new ValidationRunner(logger, new ManifestResourceManager(logger));
			runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "text"));

			var allMessages = string.Join("\n", logger.Entries.Select(e => e.Message));
			Assert.Contains("[OK]", allMessages);
			Assert.Contains("Summary:", allMessages);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_JsonFormat_ProducesValidJson() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			var logger = new TestLogger();
			var runner = new ValidationRunner(logger, new ManifestResourceManager(logger));

			runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "json"));

			var jsonLine = logger.Entries.FirstOrDefault(e => e.Message.TrimStart().StartsWith("{"));
			Assert.NotNull(jsonLine.Message);

			// 必须是合法 JSON（camelCase）
			using var doc = JsonDocument.Parse(jsonLine.Message);
			Assert.True(doc.RootElement.GetProperty("overallOk").GetBoolean());
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_CheckXelatex_XelatexOnPath_ReportsFound() {
		var (tmpDir, sourceDir, configFile) = CreateHappyPathFixture();
		try {
			var logger = new TestLogger();
			var runner = new ValidationRunner(logger, new ManifestResourceManager(logger));

			runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, CheckXelatex: true));

			var allMessages = string.Join("\n", logger.Entries.Select(e => e.Message));
			Assert.Contains("environment.xelatex", allMessages);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void CheckXelatexOnPath_EmptyPath_ReturnsFalse() {
		// 这台机器是 Linux（开发环境），xelatex 未必在 PATH，但若没有可执行文件应返回 false
		// 用空 PATH 强制 false
		var originalPath = Environment.GetEnvironmentVariable("PATH");
		try {
			Environment.SetEnvironmentVariable("PATH", "");
			Assert.False(ValidationRunner.CheckXelatexOnPath());
		} finally {
			Environment.SetEnvironmentVariable("PATH", originalPath);
		}
	}
}
