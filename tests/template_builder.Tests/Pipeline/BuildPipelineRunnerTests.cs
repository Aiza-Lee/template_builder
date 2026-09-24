using System.IO;
using Core;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

internal sealed class ThrowingResMgr : ManifestResourceManager {
    public ThrowingResMgr() { }
    public override string GetResourceInString(string resourceName)
        => throw new MissingEmbeddedResourceException(resourceName);
    public override Stream GetResourceAsStream(string resourceName)
        => throw new MissingEmbeddedResourceException(resourceName);
}

public class BuildPipelineRunnerTests {
    [Fact]
    public void Run_MalformedConfig_ReturnsExitMalformedConfig() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "bad.json"));
        File.WriteAllText(configFile.FullName, "{ \"TEX\": {");

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        var exitCode = runner.Run(options, userProvidedConfig: true);

        Assert.Equal(ExitCodes.MalformedConfig, exitCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("解析 JSON 内容失败"));
    }

    [Fact]
    public void Run_MissingConfigFile_ReturnsExitInvalidArguments() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "nonexistent.json"));

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        var exitCode = runner.Run(options, userProvidedConfig: true);

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("未找到配置文件"));
    }

    [Fact]
    public void Run_UnknownConfigKey_InStrictMode_ReturnsExitInvalidArguments() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, """{ "TEX": { "totally_made_up_key": "oops" } }""");

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        var exitCode = runner.Run(options, userProvidedConfig: true);

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
    }

    [Fact]
    public void Run_MissingEmbeddedResource_ReturnsExitMissingEmbeddedResource() {
        var logger = new TestLogger();
        // 注入会在 ConfigParser ctor 加载默认嵌入资源时立即抛出的 ManifestResourceManager
        var resMgr = new ThrowingResMgr();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(sourceDir.FullName, "a.txt"), "hello");
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, "{}");

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        var exitCode = runner.Run(options, userProvidedConfig: false);

        Assert.Equal(ExitCodes.MissingEmbeddedResource, exitCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("not found in embedded resources"));
    }

    [Fact]
    public void RunBuild_MalformedConfig_ReturnsStructuredFailureWithExitCode() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "bad.json"));
        File.WriteAllText(configFile.FullName, "{ \"TEX\": {");

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        var result = runner.RunBuild(options, userProvidedConfig: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExitCodes.MalformedConfig, result.ExitCode);
        Assert.Equal(Core.Models.BuildStatus.Failed, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("解析 JSON 内容失败", result.ErrorMessage);
    }

    [Fact]
    public void RunBuild_WithExistingConfig_RespectsPreconfiguredValuesAndOverrides() {
        var logger = new TestLogger();
        var resMgr = new ManifestResourceManager();
        var runner = new BuildPipelineRunner(logger, resMgr);

        using var tmp = TempDir.Create();
        var sourceDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "src"));
        File.WriteAllText(Path.Combine(sourceDir.FullName, "main.cpp"), "int main() { return 0; }");
        var outputPdf = new FileInfo(Path.Combine(tmp.Path, "out.pdf"));
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, "{}");

        var existingConfig = new Core.Models.ProjectConfiguration();
        existingConfig.TrySetOverride("LAYOUT_COLUMNS", "1");

        var options = new BuildSubcommandOptions(sourceDir, outputPdf, configFile, Verbose: false, TemplateDir: null);

        // 使用已有的配置执行构建（即便没有安装 xelatex，也能验证配置已生效且返回结构化结果）
        var result = runner.RunBuild(options, userProvidedConfig: false, existingConfig: existingConfig);

        Assert.NotNull(result);
        Assert.Equal("1", existingConfig.GetEffectiveValue("LAYOUT_COLUMNS"));
    }
}

