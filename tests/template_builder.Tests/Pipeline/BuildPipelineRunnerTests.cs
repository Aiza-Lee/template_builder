using System.IO;
using Core;
using Core.Pipeline;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

internal sealed class ThrowingResMgr : ManifestResourceManager {
	public ThrowingResMgr(ILogger logger) : base(logger) { }
	public override string GetResourceInString(string resourceName)
		=> throw new MissingEmbeddedResourceException(resourceName);
	public override Stream GetResourceAsStream(string resourceName)
		=> throw new MissingEmbeddedResourceException(resourceName);
}

public class BuildPipelineRunnerTests {
	[Fact]
	public void Run_MalformedConfig_ReturnsExitMalformedConfig() {
		var logger = new TestLogger();
		var resMgr = new ManifestResourceManager(logger);
		var runner = new BuildPipelineRunner(logger, resMgr);

		var tmpDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
		try {
			var sourceDir = Directory.CreateDirectory(Path.Combine(tmpDir.FullName, "src"));
			var outputPdf = new FileInfo(Path.Combine(tmpDir.FullName, "out.pdf"));
			var configFile = new FileInfo(Path.Combine(tmpDir.FullName, "bad.json"));
			File.WriteAllText(configFile.FullName, "{ \"TEX\": {");

			var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

			var exitCode = runner.Run(options, userProvidedConfig: true);

			Assert.Equal(ExitCodes.MalformedConfig, exitCode);
			Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("Failed to parse JSON content"));
		} finally {
			Directory.Delete(tmpDir.FullName, recursive: true);
		}
	}

	[Fact]
	public void Run_UnknownConfigKey_InStrictMode_ReturnsExitInvalidArguments() {
		var logger = new TestLogger();
		var resMgr = new ManifestResourceManager(logger);
		var runner = new BuildPipelineRunner(logger, resMgr);

		var tmpDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
		try {
			var sourceDir = Directory.CreateDirectory(Path.Combine(tmpDir.FullName, "src"));
			var outputPdf = new FileInfo(Path.Combine(tmpDir.FullName, "out.pdf"));
			var configFile = new FileInfo(Path.Combine(tmpDir.FullName, "config.json"));
			File.WriteAllText(configFile.FullName, """{ "TEX": { "totally_made_up_key": "oops" } }""");

			var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

			var exitCode = runner.Run(options, userProvidedConfig: true);

			Assert.Equal(ExitCodes.InvalidArguments, exitCode);
		} finally {
			Directory.Delete(tmpDir.FullName, recursive: true);
		}
	}

	[Fact]
	public void Run_MissingEmbeddedResource_ReturnsExitMissingEmbeddedResource() {
		var logger = new TestLogger();
		// 注入会在 ConfigParser ctor 加载默认嵌入资源时立即抛出的 ManifestResourceManager
		var resMgr = new ThrowingResMgr(logger);
		var runner = new BuildPipelineRunner(logger, resMgr);

		var tmpDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
		try {
			var sourceDir = Directory.CreateDirectory(Path.Combine(tmpDir.FullName, "src"));
			var outputPdf = new FileInfo(Path.Combine(tmpDir.FullName, "out.pdf"));
			var configFile = new FileInfo(Path.Combine(tmpDir.FullName, "config.json"));
			File.WriteAllText(configFile.FullName, "{}");

			var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

			var exitCode = runner.Run(options, userProvidedConfig: false);

			Assert.Equal(ExitCodes.MissingEmbeddedResource, exitCode);
			Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("not found in embedded resources"));
		} finally {
			Directory.Delete(tmpDir.FullName, recursive: true);
		}
	}
}
