using System.IO;
using System.Text.Json;
using Core;
using Core.Pipeline;
using template_builder.Tests.Fixtures;
using Utils;
using Utils.Exceptions;
using Xunit;

namespace template_builder.Tests.Pipeline;

public class ConfigInitializerTests {
    [Fact]
    public void Run_WritesConfigFileAtSpecifiedPath() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "my-config.jsonc"));
        var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

        Assert.Equal(0, exitCode);
        Assert.True(outputPath.Exists);
        Assert.True(new FileInfo(outputPath.FullName).Length > 0);
    }

    [Fact]
    public void Run_CreatesParentDirectoryIfMissing() {
        using var tmp = TempDir.Create();
        var nestedPath = new FileInfo(Path.Combine(tmp.Path, "nested", "deeper", "cfg.jsonc"));
        var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager());

        runner.Run(new InitSubcommandOptions(nestedPath));

        Assert.True(nestedPath.Exists);
    }

    [Fact]
    public void Run_JsonFormat_OutputHasNoComments() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.json"));
        var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager());

        runner.Run(new InitSubcommandOptions(outputPath, Format: "json"));

        var content = File.ReadAllText(outputPath.FullName);
        Assert.DoesNotContain("//", content);
        // 必须是合法 JSON
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("TEX", out _));
    }

    [Fact]
    public void Run_JsoncFormat_OutputContainsInlineComments() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager());

        runner.Run(new InitSubcommandOptions(outputPath, Format: "jsonc"));

        var content = File.ReadAllText(outputPath.FullName);
        Assert.Contains("//", content);  // 至少一条 inline 注释
    }

    [Fact]
    public void Run_RoundTrip_ParseAndReinit_StaysStable() {
        // 把 init 输出 r1 喂给 ConfigParser 解析，再 init 一次得到 r2，断言 r1 == r2
        //（这是 docs.json / DefaultConfig.json 漂移守卫的弱化版：保证资源本身是稳定的）
        using var tmp = TempDir.Create();
        var r1 = new FileInfo(Path.Combine(tmp.Path, "r1.jsonc"));
        var r2 = new FileInfo(Path.Combine(tmp.Path, "r2.jsonc"));
        var runner = new ConfigInitializer(new TestLogger(), new ManifestResourceManager());

        runner.Run(new InitSubcommandOptions(r1));
        // 用 ConfigParser 验证 r1 是合法配置（不会抛异常即说明 key 都已被注册）
        var parser = new ConfigParser("TEX", new TestLogger(), ConfigStrictness.Strict);
        parser.ParseConfigFile(File.ReadAllText(r1.FullName), r1.FullName);

        runner.Run(new InitSubcommandOptions(r2));

        Assert.Equal(File.ReadAllText(r1.FullName), File.ReadAllText(r2.FullName));
    }

    [Fact]
    public void Run_MissingEmbeddedResource_ReturnsMissingEmbeddedResource() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        var resMgr = new ThrowingResMgr();
        var runner = new ConfigInitializer(new TestLogger(), resMgr);

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

        Assert.Equal(5, exitCode);
    }

    [Fact]
    public void Run_ExistingFile_WithoutForce_ReturnsInvalidArguments_AndPreservesOriginal() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        const string originalContent = "{\"custom\": true}";
        File.WriteAllText(outputPath.FullName, originalContent);

        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath, Force: false));

        Assert.Equal(ExitCodes.InvalidArguments, exitCode);
        Assert.Equal(originalContent, File.ReadAllText(outputPath.FullName));
        Assert.False(File.Exists(outputPath.FullName + ".bak"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("默认拒绝写入"));
    }

    [Fact]
    public void Run_ExistingFile_WithForce_CreatesBackup_AndOverwrites() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        const string originalContent = "{\"custom\": true}";
        File.WriteAllText(outputPath.FullName, originalContent);

        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath, Force: true));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.NotEqual(originalContent, File.ReadAllText(outputPath.FullName));
        var backupPath = outputPath.FullName + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Equal(originalContent, File.ReadAllText(backupPath));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("已备份原配置至"));
    }

    [Fact]
    public void Run_DeliversSchemaFile_AlongsideConfigFile() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "sub", "my-config.jsonc"));
        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(outputPath.Exists);

        var schemaPath = Path.Combine(outputPath.Directory!.FullName, "DefaultConfig.schema.json");
        Assert.True(File.Exists(schemaPath));
        var schemaContent = File.ReadAllText(schemaPath);
        Assert.True(schemaContent.Length > 0);
        using var doc = JsonDocument.Parse(schemaContent);
        Assert.True(doc.RootElement.TryGetProperty("$schema", out _));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("配套 Schema 已交付"));
    }

    [Fact]
    public void Run_OutputGuidance_ContainsAccurateConfigPath_WithQuotes() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "my-config.jsonc"));
        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

        Assert.Equal(ExitCodes.Success, exitCode);
        var expectedArg = $"\"{outputPath.FullName.Replace("\"", "\\\"")}\"";
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO &&
            e.Message.Contains($"validate -s <源目录> -c {expectedArg}") &&
            e.Message.Contains($"build -s <源目录> -c {expectedArg} -o <输出.pdf>"));
    }

    [Fact]
    public void Run_OutputGuidance_PathWithSpaces_IsQuotedCorrectly() {
        using var tmp = TempDir.Create();
        var spaceDir = Path.Combine(tmp.Path, "sub folder with spaces");
        var outputPath = new FileInfo(Path.Combine(spaceDir, "custom config.jsonc"));
        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

        Assert.Equal(ExitCodes.Success, exitCode);
        var expectedArg = $"\"{outputPath.FullName.Replace("\"", "\\\"")}\"";
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO &&
            e.Message.Contains($"-c {expectedArg}"));
    }

    [Fact]
    public void Run_ExistingSchemaFile_WithoutForce_MatchingContent_ReusesSchema() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        var resMgr = new ManifestResourceManager();
        var schemaContent = resMgr.GetResourceInString("DefaultConfig.schema.json");
        var schemaPath = Path.Combine(tmp.Path, "DefaultConfig.schema.json");
        File.WriteAllText(schemaPath, schemaContent);

        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, resMgr);

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath, Force: false));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(File.Exists(schemaPath + ".bak"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("保持使用"));
    }

    [Fact]
    public void Run_ExistingSchemaFile_WithoutForce_CustomContent_PreservesCustomFile() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        const string customSchema = "{\"custom_schema\": true}";
        var schemaPath = Path.Combine(tmp.Path, "DefaultConfig.schema.json");
        File.WriteAllText(schemaPath, customSchema);

        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath, Force: false));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(customSchema, File.ReadAllText(schemaPath));
        Assert.False(File.Exists(schemaPath + ".bak"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("已保留现有文件"));
    }

    [Fact]
    public void Run_ExistingSchemaFile_WithForce_BacksUpAndOverwritesSchema() {
        using var tmp = TempDir.Create();
        var outputPath = new FileInfo(Path.Combine(tmp.Path, "cfg.jsonc"));
        const string customSchema = "{\"custom_schema\": true}";
        var schemaPath = Path.Combine(tmp.Path, "DefaultConfig.schema.json");
        File.WriteAllText(schemaPath, customSchema);

        var logger = new TestLogger();
        var runner = new ConfigInitializer(logger, new ManifestResourceManager());

        var exitCode = runner.Run(new InitSubcommandOptions(outputPath, Force: true));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(schemaPath + ".bak"));
        Assert.Equal(customSchema, File.ReadAllText(schemaPath + ".bak"));
        Assert.NotEqual(customSchema, File.ReadAllText(schemaPath));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.INFO && e.Message.Contains("已备份原文件至"));
    }

    [Fact]
    public void FormatPathForCli_EscapesInnerQuotesAndWrapsInQuotes() {
        var simple = new FileInfo("test.jsonc");
        Assert.Equal("\"test.jsonc\"", ConfigInitializer.FormatPathForCli(simple));

        var spaces = new FileInfo("path with spaces/config.jsonc");
        Assert.Equal("\"path with spaces/config.jsonc\"", ConfigInitializer.FormatPathForCli(spaces));
    }

    [Fact]
    public void Run_UnwritableDirectory_ReturnsInvalidArguments() {
        if (OperatingSystem.IsWindows()) return;
        using var tmp = TempDir.Create();
        var readOnlyDir = Directory.CreateDirectory(Path.Combine(tmp.Path, "readonly"));
        var outputPath = new FileInfo(Path.Combine(readOnlyDir.FullName, "cfg.jsonc"));
        try {
            File.SetUnixFileMode(readOnlyDir.FullName, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var logger = new TestLogger();
            var runner = new ConfigInitializer(logger, new ManifestResourceManager());

            var exitCode = runner.Run(new InitSubcommandOptions(outputPath));

            Assert.Equal(ExitCodes.InvalidArguments, exitCode);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.ERROR && e.Message.Contains("无访问权限"));
        } finally {
            File.SetUnixFileMode(readOnlyDir.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
