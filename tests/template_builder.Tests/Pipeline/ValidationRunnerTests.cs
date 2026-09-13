using System.IO;
using System.Text.Json;
using Core;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ValidationRunnerTests {
    private static (TempDir tmp, DirectoryInfo sourceDir, FileInfo configFile) CreateHappyPathFixture() {
        var tmp = TempDir.Create();
        var sourceDir = new DirectoryInfo(Path.Combine(tmp.Path, "src"));
        sourceDir.Create();
        File.WriteAllText(Path.Combine(sourceDir.FullName, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(sourceDir.FullName, "b.txt"), "beta");
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        // 默认 DefaultConfig 即可：包含 .txt 的 include_file_types 与空的 ignore_patterns
        File.WriteAllText(configFile.FullName, "{}");
        return (tmp, sourceDir, configFile);
    }

    [Fact]
    public void Run_HappyPath_EmptySource_ReturnsOk() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        sourceDir.Delete(recursive: true);
        sourceDir.Create();  // 重新建一个空目录
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

        Assert.Equal(0, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_NonExistentSourceDir_ReturnsInvalidArguments() {
        var (tmp, _, configFile) = CreateHappyPathFixture();
        var missingDir = new DirectoryInfo(Path.Combine(tmp.Path, "does-not-exist"));
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(missingDir, configFile));

        Assert.Equal(2, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_MalformedConfig_ReturnsMalformedConfig() {
        var (tmp, sourceDir, _) = CreateHappyPathFixture();
        var configFile = new FileInfo(Path.Combine(tmp.Path, "bad.json"));
        File.WriteAllText(configFile.FullName, "{ \"TEX\": {");
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

        Assert.Equal(4, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_UnknownConfigKey_ReturnsInvalidArguments() {
        var (tmp, sourceDir, _) = CreateHappyPathFixture();
        var configFile = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        File.WriteAllText(configFile.FullName, """{ "TEX": { "totally_made_up_key": "oops" } }""");
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

        Assert.Equal(2, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_MissingEmbeddedResource_ReturnsMissingEmbeddedResource() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        var resMgr = new ThrowingResMgr();
        var runner = new ValidationRunner(new TestLogger(), resMgr);

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

        Assert.Equal(5, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_MissingPlaceholderInTemplateDir_ReturnsUnresolvedPlaceholders() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        // 用一个外部 template-dir 提供带 ##MISSING## 的 Main.tex
        var templateDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "templates"));
        File.WriteAllText(Path.Combine(templateDir.FullName, "Main.tex"),
            "\\documentclass{article}\\title{##MISSING##}\\begin{document}##AUTHOR##\\end{document}");
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, templateDir));

        Assert.Equal(3, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_SixLevelDeepDirectory_ReturnsValidationFailed() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        // 建 6 层深目录：src/a/b/c/d/e/f/
        var deep = sourceDir.FullName;
        for (int i = 0; i < 6; i++) {
            deep = Path.Combine(deep, $"level{i}");
            Directory.CreateDirectory(deep);
        }
        var runner = new ValidationRunner(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile));

        Assert.Equal(6, exitCode);
        tmp.Dispose();
    }

    [Fact]
    public void Run_TextFormat_ContainsOkAndErrorMarkers() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager());

        runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "text"));

        var allMessages = string.Join("\n", logger.Entries.Select(e => e.Message));
        Assert.Contains("[通过]", allMessages);
        Assert.Contains("汇总：", allMessages);
        tmp.Dispose();
    }

    [Fact]
    public void Run_JsonFormat_ProducesValidJson() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager());

        runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "json"));

        var jsonLine = logger.Entries.FirstOrDefault(e => e.Message.TrimStart().StartsWith("{"));
        Assert.NotNull(jsonLine.Message);

        // 必须是合法 JSON（camelCase）
        using var doc = JsonDocument.Parse(jsonLine.Message);
        Assert.True(doc.RootElement.GetProperty("overallOk").GetBoolean());
        tmp.Dispose();
    }

    [Fact]
    public void Run_CheckXelatex_XelatexOnPath_ReportsFound() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager());

        runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, CheckXelatex: true));

        var allMessages = string.Join("\n", logger.Entries.Select(e => e.Message));
        Assert.Contains("environment.xelatex", allMessages);
        tmp.Dispose();
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
