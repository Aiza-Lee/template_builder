using System.IO;
using System.Text.Json;
using Core;
using Core.Pipeline;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ConfigInitializerTests {
	private static string CreateTempDir() {
		return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
	}

	[Fact]
	public void Run_WritesConfigFileAtSpecifiedPath() {
		var tmpDir = CreateTempDir();
		try {
			var outputPath = new FileInfo(Path.Combine(tmpDir, "my-config.jsonc"));
			var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

			Assert.Equal(0, exitCode);
			Assert.True(outputPath.Exists);
			Assert.True(new FileInfo(outputPath.FullName).Length > 0);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_CreatesParentDirectoryIfMissing() {
		var tmpDir = CreateTempDir();
		try {
			var nestedPath = new FileInfo(Path.Combine(tmpDir, "nested", "deeper", "cfg.jsonc"));
			var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			runner.Run(new InitSubcommandOptions(nestedPath));

			Assert.True(nestedPath.Exists);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_JsonFormat_OutputHasNoComments() {
		var tmpDir = CreateTempDir();
		try {
			var outputPath = new FileInfo(Path.Combine(tmpDir, "cfg.json"));
			var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			runner.Run(new InitSubcommandOptions(outputPath, Format: "json"));

			var content = File.ReadAllText(outputPath.FullName);
			Assert.DoesNotContain("//", content);
			// 必须是合法 JSON
			using var doc = JsonDocument.Parse(content);
			Assert.True(doc.RootElement.TryGetProperty("TEX", out _));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_JsoncFormat_OutputContainsInlineComments() {
		var tmpDir = CreateTempDir();
		try {
			var outputPath = new FileInfo(Path.Combine(tmpDir, "cfg.jsonc"));
			var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			runner.Run(new InitSubcommandOptions(outputPath, Format: "jsonc"));

			var content = File.ReadAllText(outputPath.FullName);
			Assert.Contains("//", content);  // 至少一条 inline 注释
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_RoundTrip_ParseAndReinit_StaysStable() {
		// 把 init 输出 r1 喂给 ConfigParser 解析，再 init 一次得到 r2，断言 r1 == r2
		//（这是 docs.json / DefaultConfig.json 漂移守卫的弱化版：保证资源本身是稳定的）
		var tmpDir = CreateTempDir();
		try {
			var r1 = new FileInfo(Path.Combine(tmpDir, "r1.jsonc"));
			var r2 = new FileInfo(Path.Combine(tmpDir, "r2.jsonc"));
			var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager(new TestLogger()));

			runner.Run(new InitSubcommandOptions(r1));
			// 用 ConfigParser 验证 r1 是合法配置（不会抛异常即说明 key 都已被注册）
			var parser = new ConfigParser("TEX", new TestLogger(), ConfigStrictness.Strict);
			parser.ParseConfigFile(File.ReadAllText(r1.FullName), r1.FullName);

			runner.Run(new InitSubcommandOptions(r2));

			Assert.Equal(File.ReadAllText(r1.FullName), File.ReadAllText(r2.FullName));
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}

	[Fact]
	public void Run_MissingEmbeddedResource_ReturnsMissingEmbeddedResource() {
		var tmpDir = CreateTempDir();
		try {
			var outputPath = new FileInfo(Path.Combine(tmpDir, "cfg.jsonc"));
			var resMgr = new ThrowingResMgr(new TestLogger());
			var runner = new ConfigInitializer(new TestLogger(), resMgr);

			var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

			Assert.Equal(5, exitCode);
		} finally {
			Directory.Delete(tmpDir, recursive: true);
		}
	}
}
