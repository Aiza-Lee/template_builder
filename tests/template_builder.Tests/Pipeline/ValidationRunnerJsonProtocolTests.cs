using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Core;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ValidationRunnerJsonProtocolTests {
    private static (TempDir Tmp, DirectoryInfo SourceDir, FileInfo ConfigFile) CreateHappyPathFixture() {
        var tmp = TempDir.Create();
        var sourceDir = new DirectoryInfo(Path.Combine(tmp.Path, "src"));
        sourceDir.Create();
        File.WriteAllText(Path.Combine(sourceDir.FullName, "main.txt"), "hello world");
        var configFile = new FileInfo(Path.Combine(tmp.Path, "config.json"));
        File.WriteAllText(configFile.FullName, "{}");
        return (tmp, sourceDir, configFile);
    }

    [Fact]
    public void Run_JsonFormat_HappyPath_StdoutIsPureValidJsonWithoutLogPrefix() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        using var stdoutWriter = new StringWriter();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager(), stdoutWriter);

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "json"));

        Assert.Equal(ExitCodes.Success, exitCode);
        var stdout = stdoutWriter.ToString().Trim();

        // 验证 stdout 必须纯净且可被标准 JsonDocument 反序列化
        Assert.StartsWith("{", stdout);
        Assert.EndsWith("}", stdout);
        Assert.DoesNotContain("[Error]", stdout);
        Assert.DoesNotContain("[INFO]", stdout);
        Assert.DoesNotContain("[Warning]", stdout);
        Assert.DoesNotContain("使用配置文件", stdout);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("overallOk").GetBoolean());
        Assert.Equal(0, root.GetProperty("errorCount").GetInt32());
        Assert.True(root.GetProperty("checks").GetArrayLength() > 0);
        tmp.Dispose();
    }

    [Fact]
    public void Run_JsonFormat_FailurePath_StdoutIsPureValidJsonWithoutErrorPrefix() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        // 注入损坏的 JSON 配置
        File.WriteAllText(configFile.FullName, "{ \"TEX\": {");
        using var stdoutWriter = new StringWriter();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager(), stdoutWriter);

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "json"));

        Assert.Equal(ExitCodes.MalformedConfig, exitCode);
        var stdout = stdoutWriter.ToString().Trim();

        // 绝不可带有 [Error] 前缀
        Assert.StartsWith("{", stdout);
        Assert.EndsWith("}", stdout);
        Assert.DoesNotContain("[Error]", stdout);

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("overallOk").GetBoolean());
        Assert.True(root.GetProperty("errorCount").GetInt32() > 0);

        var checks = root.GetProperty("checks").EnumerateArray().ToList();
        var parseCheck = checks.FirstOrDefault(c => c.GetProperty("category").GetString() == "config" && c.GetProperty("name").GetString() == "parses");
        Assert.False(parseCheck.GetProperty("ok").GetBoolean());
        tmp.Dispose();
    }

    [Fact]
    public void Run_JsonFormat_MissingSourceDir_ProducesStructuredJsonAndReturnsExitCode2() {
        var (tmp, _, configFile) = CreateHappyPathFixture();
        var missingDir = new DirectoryInfo(Path.Combine(tmp.Path, "no_such_source_folder"));
        using var stdoutWriter = new StringWriter();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager(), stdoutWriter);

        var exitCode = runner.Run(new ValidateSubcommandOptions(missingDir, configFile, Format: "json"));

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        var stdout = stdoutWriter.ToString().Trim();

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("overallOk").GetBoolean());
        Assert.Equal(1, root.GetProperty("errorCount").GetInt32());

        var firstCheck = root.GetProperty("checks").EnumerateArray().First();
        Assert.Equal("source", firstCheck.GetProperty("category").GetString());
        Assert.Equal("exists", firstCheck.GetProperty("name").GetString());
        Assert.False(firstCheck.GetProperty("ok").GetBoolean());
        tmp.Dispose();
    }

    [Fact]
    public void Run_JsonFormat_MissingConfigFile_ProducesStructuredJsonAndReturnsExitCode2() {
        var (tmp, sourceDir, _) = CreateHappyPathFixture();
        var missingConfigFile = new FileInfo(Path.Combine(tmp.Path, "missing_cfg.json"));
        using var stdoutWriter = new StringWriter();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager(), stdoutWriter);

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, missingConfigFile, Format: "json"));

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        var stdout = stdoutWriter.ToString().Trim();

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("overallOk").GetBoolean());

        var checks = root.GetProperty("checks").EnumerateArray().ToList();
        var cfgCheck = checks.First(c => c.GetProperty("category").GetString() == "config");
        Assert.Equal("exists", cfgCheck.GetProperty("name").GetString());
        Assert.False(cfgCheck.GetProperty("ok").GetBoolean());
        tmp.Dispose();
    }

    [Fact]
    public void Run_InvalidFormat_ReturnsExitCode2AndMentionsLegalValues() {
        var (tmp, sourceDir, configFile) = CreateHappyPathFixture();
        var logger = new TestLogger();
        var runner = new ValidationRunner(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new ValidateSubcommandOptions(sourceDir, configFile, Format: "xml"));

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        Assert.Contains(logger.ErrorMessages, m => m.Contains("--format") && m.Contains("text") && m.Contains("json"));
        tmp.Dispose();
    }

    [Fact]
    public void DiagnosticStderrLogger_RedirectsDiagnosticsToErrorWriter() {
        using var errWriter = new StringWriter();
        var inner = new TestLogger();
        var diagLogger = new DiagnosticStderrLogger(inner, errWriter);
        diagLogger.SetLevel(LogLevel.DEBUG);

        diagLogger.Info("使用配置文件：\"/tmp/cfg.json\"。");
        diagLogger.Warning("测试告警");
        diagLogger.Error("测试错误");
        diagLogger.Debug("测试调试");

        var errOutput = errWriter.ToString();
        Assert.Contains("使用配置文件：\"/tmp/cfg.json\"。", errOutput);
        Assert.Contains("[Warning] 测试告警", errOutput);
        Assert.Contains("[Error] 测试错误", errOutput);
        Assert.Contains("[Debug] 测试调试", errOutput);
    }

    [Fact]
    public void Logger_WarningAndError_GoToErrorWriter() {
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        var logger = new Logger(outWriter, errWriter);

        logger.Info("正常消息");
        logger.Warning("告警消息");
        logger.Error("错误消息");

        Assert.Contains("正常消息", outWriter.ToString());
        Assert.DoesNotContain("告警消息", outWriter.ToString());
        Assert.DoesNotContain("错误消息", outWriter.ToString());

        Assert.Contains("[Warning] 告警消息", errWriter.ToString());
        Assert.Contains("[Error] 错误消息", errWriter.ToString());
    }
}
