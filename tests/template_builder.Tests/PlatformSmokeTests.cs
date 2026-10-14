using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Core.Commands;
using template_builder.Tests.Fixtures;
using Utils;
using Xunit;

namespace template_builder.Tests;

/// <summary>
/// 跨平台运行验证门禁测试套件 (RC-15)。
/// 验证在当前平台执行环境下的命令行入口契约、环境感知、JSON 协议及路径行为。
/// </summary>
public class PlatformSmokeTests {
    [Fact]
    public void PlatformEnvironment_IsSupportedAndResolvable() {
        // 验证当前操作系统属于受支持平台 (Linux / Windows / OSX)
        bool isSupported = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                           RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                           RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        Assert.True(isSupported, $"Unsupported OS platform: {RuntimeInformation.OSDescription}");

        // 验证用户配置路径在此平台下能正常解析而不抛出异常
        var helper = new UserConfigPathHelper("NightingaleStudio", "TemplateBuilder");
        string configPath = helper.GetUserConfigPath();
        Assert.False(string.IsNullOrWhiteSpace(configPath));
        Assert.True(Path.IsPathRooted(configPath));
    }

    [Fact]
    public void CliSmoke_InitAndValidate_WorksOnCurrentPlatform() {
        using var tmp = TempDir.Create();
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        // 1. init 生成配置与 schema
        string configPath = Path.Combine(tmp.Path, "platform_config.jsonc");
        int initCode = root.Parse(["init", "-o", configPath]).Invoke();
        Assert.Equal(0, initCode);
        Assert.True(File.Exists(configPath));
        string schemaPath = Path.Combine(tmp.Path, "DefaultConfig.schema.json");
        Assert.True(File.Exists(schemaPath));

        // 2. 准备源文件目录
        string srcDir = Path.Combine(tmp.Path, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "solution.cpp"), "int main() { return 0; }");

        // 3. validate 输出 JSON 验证
        using var sw = new StringWriter();
        var origOut = Console.Out;
        int validateCode;
        try {
            Console.SetOut(sw);
            validateCode = root.Parse(["validate", "-s", srcDir, "-c", configPath, "--format", "json"]).Invoke();
        } finally {
            Console.SetOut(origOut);
        }

        Assert.Equal(0, validateCode);
        string jsonOutput = sw.ToString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(jsonOutput));

        using var doc = JsonDocument.Parse(jsonOutput);
        var rootElem = doc.RootElement;
        Assert.True(rootElem.GetProperty("overallOk").GetBoolean());
        Assert.Equal(0, rootElem.GetProperty("errorCount").GetInt32());
    }

    [Fact]
    public void CliSmoke_HelpAndVersionOptions_ExitSuccessfully() {
        var factory = new RootCommandFactory(new TestLogger());
        var root = factory.CreateRootCommand();

        int helpCode = root.Parse(["--help"]).Invoke();
        Assert.Equal(0, helpCode);

        int versionCode = root.Parse(["--version"]).Invoke();
        Assert.Equal(0, versionCode);
    }
}
