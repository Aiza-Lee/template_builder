using System.CommandLine;
using System.IO;
using System.Linq;
using Core;
using Core.Commands;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests;

public class RootCommandFactoryTests {
    [Fact]
    public void CreateRootCommand_ExposesAllThreeSubcommands() {
        var factory = new RootCommandFactory(new TestLogger());

        var root = factory.CreateRootCommand();

        var subcommandNames = root.Subcommands.Select(c => c.Name).ToHashSet();
        Assert.Contains("build", subcommandNames);
        Assert.Contains("validate", subcommandNames);
        Assert.Contains("init", subcommandNames);
    }

    [Fact]
    public void CreateRootCommand_BuildSubcommand_ExposesAllExpectedOptions() {
        var factory = new RootCommandFactory(new TestLogger());

        var root = factory.CreateRootCommand();
        var build = root.Subcommands.Single(c => c.Name == "build");
        var names = build.Options.Select(o => o.Name).ToHashSet();
        var aliases = build.Options.SelectMany(o => o.Aliases).ToHashSet();

        Assert.Contains("--source-files-folder", names);
        Assert.Contains("-s", aliases);
        Assert.Contains("--output", names);
        Assert.Contains("-o", aliases);
        Assert.Contains("--verbose", names);
        Assert.Contains("-v", aliases);
        Assert.Contains("--config", names);
        Assert.Contains("-c", aliases);
        Assert.Contains("--template-dir", names);
        Assert.Contains("-t", aliases);
    }

    [Fact]
    public void CreateRootCommand_BuildSubcommand_ExposesTuiOption() {
        var factory = new RootCommandFactory(new TestLogger());

        var root = factory.CreateRootCommand();
        var build = root.Subcommands.Single(c => c.Name == "build");
        var names = build.Options.Select(o => o.Name).ToHashSet();

        Assert.Contains("--tui", names);
    }

    [Fact]
    public void Invoke_Build_WithoutSourceAndOutput_ReturnsInvalidArguments() {
        // -s / -o 改为可选（供 --tui 交互输入）后，非 --tui 路径必须显式报缺参，
        // 且退出码为 InvalidArguments(2) 而非解析层的 1
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        var result = root.Parse(new[] { "build" }).Invoke();

        Assert.Equal(2, result);
    }

    [Fact]
    public void Invoke_Build_WithNonExistentSourceFolder_ReturnsNonZero() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        var outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

        var result = root.Parse(new[] { "build", "-s", "/no/such/source/folder", "-o", outputPath }).Invoke();

        Assert.NotEqual(0, result);
    }

    [Fact]
    public void Invoke_NoSubcommand_ReturnsInvalidArguments() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        var result = root.Parse(Array.Empty<string>()).Invoke();

        // ExitCodes.InvalidArguments = 2
        Assert.Equal(2, result);
    }

    [Fact]
    public void Invoke_Validate_IsWiredUpToStub() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        var result = root.Parse(new[] { "validate", "-s", "/tmp", "-c", "/tmp/cfg.json" }).Invoke();

        // stub: not yet implemented → non-zero
        Assert.NotEqual(0, result);
    }

    [Fact]
    public void Invoke_Validate_WithoutConfig_UsesDefaultConfig() {
        // validate -c 现已可选；省略时应走 ConfigPathResolver 默认路径，而非直接 crash/InvalidArguments
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);

        // 无 -c 参数 → 使用默认配置；源目录存在但可能没有配置文件(第一次运行会自动创建)
        // 这里只验证返回码不是 2 (InvalidArguments)，以排除"参数缺失"错误
        var result = root.Parse(new[] { "validate", "-s", srcDir }).Invoke();

        Assert.NotEqual(2, result);
    }


    [Fact]
    public void Invoke_Init_WritesConfigFileAndReturnsSuccess() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var outputPath = Path.Combine(tmp.Path, "cfg.jsonc");

        var result = root.Parse(new[] { "init", "-o", outputPath }).Invoke();

        Assert.Equal(0, result);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Invoke_Init_ExistingFile_WithoutForce_ReturnsInvalidArguments() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var outputPath = Path.Combine(tmp.Path, "cfg.jsonc");
        File.WriteAllText(outputPath, "{\"keep\": 1}");

        var result = root.Parse(new[] { "init", "-o", outputPath }).Invoke();

        Assert.Equal(ExitCodes.InvalidArguments, result);
        Assert.Equal("{\"keep\": 1}", File.ReadAllText(outputPath));
    }

    [Fact]
    public void Invoke_Init_ExistingFile_WithForce_CreatesBackupAndOverwrites() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var outputPath = Path.Combine(tmp.Path, "cfg.jsonc");
        File.WriteAllText(outputPath, "{\"keep\": 1}");

        var result = root.Parse(new[] { "init", "-o", outputPath, "--force" }).Invoke();

        Assert.Equal(0, result);
        Assert.NotEqual("{\"keep\": 1}", File.ReadAllText(outputPath));
        Assert.True(File.Exists(outputPath + ".bak"));
        Assert.Equal("{\"keep\": 1}", File.ReadAllText(outputPath + ".bak"));
    }

    [Fact]
    public void GetDefaultConfigFileInfo_ReturnsPathWithoutCreatingFile() {
        var info = RootCommandFactory.GetDefaultConfigFileInfo();
        Assert.NotNull(info);
        Assert.EndsWith("config.json", info.FullName);
    }

    [Fact]
    public void Invoke_Build_WithExplicitExistingConfig_DoesNotTriggerDefaultConfigCreation() {
        var logger = new TestLogger();
        var factory = new RootCommandFactory(logger);
        var root = factory.CreateRootCommand();

        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        var cfgFile = Path.Combine(tmp.Path, "custom.json");
        File.WriteAllText(cfgFile, "{}");
        var outPdf = Path.Combine(tmp.Path, "out.pdf");

        root.Parse(new[] { "build", "-s", srcDir, "-o", outPdf, "-c", cfgFile }).Invoke();

        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("创建默认配置文件"));
    }

    [Fact]
    public void Invoke_Validate_InvalidFormat_ReturnsExitCode2() {
        var logger = new TestLogger();
        var factory = new RootCommandFactory(logger);
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);

        var result = root.Parse(new[] { "validate", "-s", srcDir, "--format", "xml" }).Invoke();

        Assert.Equal(2, result);
        Assert.Contains(logger.ErrorMessages, m => m.Contains("--format") && m.Contains("text") && m.Contains("json"));
    }

    [Fact]
    public void Invoke_Init_InvalidFormat_ReturnsExitCode2AndDoesNotCreateFile() {
        var logger = new TestLogger();
        var factory = new RootCommandFactory(logger);
        var root = factory.CreateRootCommand();
        using var tmp = TempDir.Create();
        var outputPath = Path.Combine(tmp.Path, "cfg.json");

        var result = root.Parse(new[] { "init", "-o", outputPath, "--format", "xml" }).Invoke();

        Assert.Equal(2, result);
        Assert.False(File.Exists(outputPath));
        Assert.Contains(logger.ErrorMessages, m => m.Contains("--format") && m.Contains("jsonc") && m.Contains("json"));
    }

    [Fact]
    public void Invoke_Validate_JsonFormat_HappyPath_PureJsonOnStdout() {
        var logger = new TestLogger();
        using var capturedOut = new StringWriter();
        using var capturedError = new StringWriter();
        var factory = new RootCommandFactory(logger, capturedOut, capturedError);
        var root = factory.CreateRootCommand();

        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "test.txt"), "hello");
        var cfgFile = Path.Combine(tmp.Path, "config.json");
        File.WriteAllText(cfgFile, "{}");

        var result = root.Parse(new[] { "validate", "-s", srcDir, "-c", cfgFile, "--format", "json" }).Invoke();

        Assert.Equal(0, result);
        var stdout = capturedOut.ToString().Trim();

        // stdout 必须 100% 纯净可解析
        Assert.StartsWith("{", stdout);
        Assert.EndsWith("}", stdout);
        Assert.DoesNotContain("使用配置文件", stdout);
        Assert.DoesNotContain("[Error]", stdout);

        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("overallOk").GetBoolean());

        // 诊断日志应在 stderr
        var stderr = capturedError.ToString();
        Assert.Contains("使用配置文件", stderr);
    }

    [Fact]
    public void Invoke_Validate_JsonFormat_NonExistentSource_PureJsonOnStdoutAndExitCode2() {
        var logger = new TestLogger();
        using var capturedOut = new StringWriter();
        var factory = new RootCommandFactory(logger, capturedOut);
        var root = factory.CreateRootCommand();

        var result = root.Parse(new[] { "validate", "-s", "/non/existent/src/dir", "--format", "json" }).Invoke();

        Assert.Equal(2, result);
        var stdout = capturedOut.ToString().Trim();

        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("overallOk").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("errorCount").GetInt32() > 0);
    }

    [Fact]
    public void Invoke_Validate_JsonFormat_NonExistentExplicitConfig_PureJsonOnStdoutAndExitCode2() {
        var logger = new TestLogger();
        using var capturedOut = new StringWriter();
        var factory = new RootCommandFactory(logger, capturedOut);
        var root = factory.CreateRootCommand();

        using var tmp = TempDir.Create();
        var srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "test.txt"), "hello");

        var result = root.Parse(new[] { "validate", "-s", srcDir, "-c", "/non/existent/cfg.json", "--format", "json" }).Invoke();

        Assert.Equal(2, result);
        var stdout = capturedOut.ToString().Trim();

        using var doc = System.Text.Json.JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("overallOk").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("errorCount").GetInt32() > 0);
    }
}
