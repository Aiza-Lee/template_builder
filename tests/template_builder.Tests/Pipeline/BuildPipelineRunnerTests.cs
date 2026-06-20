using System.IO;
using Core;
using Core.Pipeline;
using Utils;
using Xunit;

namespace template_builder.Tests.Pipeline;

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

			var options = new BuildOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

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

			var options = new BuildOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

			var exitCode = runner.Run(options, userProvidedConfig: true);

			Assert.Equal(ExitCodes.InvalidArguments, exitCode);
		} finally {
			Directory.Delete(tmpDir.FullName, recursive: true);
		}
	}
}
